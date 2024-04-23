using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;


namespace DotNetVectorDemo
{
    class Program
    {

        // Constants for the model name and dimensions, and the semantic search configuration name
        private const string ModelName = "embedding";
        private const string ChatModelName = "chat2";
        private const int ModelDimensions = 1536;
        private const string SemanticSearchConfigName = "my-semantic-config";

        public static object? JsonConvert { get; private set; }

        // Declare a static HttpClient instance. HttpClient is intended to be instantiated once and reused throughout the life of an application.
        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {

            // Load configuration settings from local.settings.json
            var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("local.settings.json").Build();

            // Load environment variables
            var serviceEndpoint = configuration["AZURE_SEARCH_SERVICE_ENDPOINT"] ?? string.Empty;
            var indexName = configuration["AZURE_SEARCH_INDEX_NAME"] ?? string.Empty;
            var key = configuration["AZURE_SEARCH_ADMIN_KEY"] ?? string.Empty;
            var openaiApiKey = configuration["AZURE_OPENAI_API_KEY"] ?? string.Empty;
            var openaiEndpoint = configuration["AZURE_OPENAI_ENDPOINT"] ?? string.Empty;

            // Initialize OpenAI client
            var credential = new AzureKeyCredential(openaiApiKey);
            var openAIClient = new OpenAIClient(new Uri(openaiEndpoint), credential);

            // Initialize Azure Cognitive Search clients
            var searchCredential = new AzureKeyCredential(key);
            var indexClient = new SearchIndexClient(new Uri(serviceEndpoint), searchCredential);
            var searchClient = indexClient.GetSearchClient(indexName);

            // Ask user if they want to index documents
            Console.Write("Would you like to create a search index, it will read content of the JSON file, generate embeddings, and upload the doc to the search index (y/n)? ");
            string indexChoice = Console.ReadLine()?.ToLower() ?? string.Empty;
            if (indexChoice == "y")
            {
                // Debug: Creating the search index
                indexClient.CreateOrUpdateIndex(GetSampleIndex(indexName));
                Console.WriteLine("Search index created.");

                // Read the entire content of the JSON file into a string
                var inputJson = File.ReadAllText("../data/sample-resume.json");

                // Deserialize the JSON string into a list of dictionaries.
                var inputDocuments = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(inputJson) ?? new List<Dictionary<string, object>>();

                // Call the GetSampleDocumentsAsync method to generate embeddings for the documents.
                var sampleDocuments = await GetSampleDocumentsAsync(openAIClient, inputDocuments);
                Console.WriteLine("****Embeddings generated for input documents.");

                // Debug: Uploading documents to search index
                await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(sampleDocuments));
                Console.WriteLine("****Documents uploaded to the search index.");

            }

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("Choose a query approach:");
                Console.WriteLine("1. Single Vector Search");
                Console.WriteLine("2. Single Vector Search with Filter");
                Console.WriteLine("3. Simple Hybrid Search");
                Console.WriteLine("4. Semantic Hybrid Search");
                Console.WriteLine("5. Chatgpt with OpenAI client");
                Console.WriteLine("6. Chatgpt with OWN DATA");

                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Enter the number of the option: ");
                Console.ResetColor();
                int choice = int.Parse(Console.ReadLine() ?? "0");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("Type a  search query or prompt: ");
                Console.ResetColor();
                string inputQuery = Console.ReadLine() ?? string.Empty;


                // Perform search based on user's choice
                switch (choice)
                {
                    case 1:
                        await SingleVectorSearch(searchClient, openAIClient, inputQuery);
                        break;
                    case 2:
                        Console.Write("Enter a filter for the search (e.g., category eq 'Databases'): ");
                        string filter = Console.ReadLine() ?? string.Empty;
                        await SingleVectorSearchWithFilter(searchClient, openAIClient, inputQuery, filter);
                        break;
                    case 3:
                        await SimpleHybridSearch(searchClient, openAIClient, inputQuery);
                        break;
                    case 4:
                        await SemanticHybridSearch(searchClient, openAIClient, inputQuery);
                        break;
                    case 5:
                        await Gpt3Chat(inputQuery, openAIClient);
                        break;
                    case 6:
                        await ChatMyData(inputQuery, serviceEndpoint, indexName, key, openaiApiKey, openaiEndpoint);
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Exiting...");
                        break;
                }

                Console.Write("Press Enter to continue, or type 'n' to exit: ");
                string? response = Console.ReadLine();
                if (response?.ToLower() == "n")
                {
                    break;
                }
            }

        }

        // Function to generate embeddings
        private static async Task<IReadOnlyList<float>> GenerateEmbeddings(string text, OpenAIClient openAIClient)
        {
           // Console.WriteLine($"Generating embeddings for text: {text}");

            /// Call the GetEmbeddingsAsync method of the OpenAIClient instance, passing in the model name and a new instance of EmbeddingsOptions with the text to be processed.
            // This method generates embeddings for the provided text using the specified model, and returns a response containing the embeddings.
            var response = await openAIClient.GetEmbeddingsAsync(ModelName, new EmbeddingsOptions(text));
            Console.WriteLine($"Embeddings generated successfully.for text");
            return response.Value.Data[0].Embedding;
        }


        /// <summary>
        /// Gpt3Chat function that performs a chat conversation using the OpenAI API.
        /// </summary>
        /// <param name="inputQuery">The user's input query.</param>
        /// <param name="openAIClient">The OpenAIClient object used to interact with the OpenAI API.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task Gpt3Chat(string inputQuery, OpenAIClient openAIClient)
        {
            var chatOptions = new ChatCompletionsOptions()
            {
                Messages =
                {
                    new ChatMessage(ChatRole.System, """  """),
                    new ChatMessage(ChatRole.User, "What's your name?"),
                    new ChatMessage(ChatRole.Assistant, "Hi, my name is Eddie AI! Nice to meet you."),
                    new ChatMessage(ChatRole.User, inputQuery),
                }
            };

            // Asynchronously request chat completions from the OpenAI API using the specified model and options. The result is stored in the 'chatCompletion' variable.
            var chatCompletion = await openAIClient.GetChatCompletionsAsync(ChatModelName, chatOptions);
            Console.ForegroundColor = ConsoleColor.Green;


            Console.WriteLine("AI'S RESPONSE:  " + chatCompletion.Value.Choices[0].Message.Content);
            Console.ResetColor();


        }

        /// <summary>
        /// ChatMyData function that interacts with an AI model to generate a response based on an initial message.
        /// </summary>
        /// <param name="initialMessage">The initial message to start the conversation.</param>
        /// <param name="serviceEndpoint">The endpoint of the Azure Cognitive Search service.</param>
        /// <param name="indexName">The name of the index in the Azure Cognitive Search service.</param>
        /// <param name="searchApiKey">The API key for the Azure Cognitive Search service.</param>
        /// <param name="openaiApiKey">The API key for the OpenAI service.</param>
        /// <param name="openaiEndpoint">The endpoint of the OpenAI service.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task ChatMyData(string initialMessage, string serviceEndpoint, string indexName, string searchApiKey, string openaiApiKey, string openaiEndpoint)
        {
            httpClient.DefaultRequestHeaders.Add("api-key", openaiApiKey);

            var requestContent = new StringContent(JsonSerializer.Serialize(new
            {
                temperature = 0,
                max_tokens = 1000,
                top_p = 1.0,
                dataSources = new[] { new { type = "AzureCognitiveSearch", parameters = new { endpoint = serviceEndpoint, key = searchApiKey, indexName = indexName } } },
                messages = new[] { new { role = "user", content = initialMessage } }
            }), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{openaiEndpoint}/openai/deployments/chat2/extensions/chat/completions?api-version=2023-06-01-preview", requestContent);

            var responseObject = JsonSerializer.Deserialize<Dictionary<string, object>>(await response.Content.ReadAsStringAsync());

            if (responseObject.TryGetValue("choices", out object? value) && value is JsonElement choicesElement)
            {
                foreach (var choice in choicesElement.EnumerateArray())
                {
                    if (choice.TryGetProperty("messages", out var messagesElement))
                    {
                        foreach (var message in messagesElement.EnumerateArray())
                        {
                            if (message.GetProperty("role").GetString() == "assistant" && message.TryGetProperty("content", out var contentElement))
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("AI'S RESPONSE:  " + contentElement.GetString());
                                Console.ResetColor();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Performs a single vector search using the specified search client and OpenAI client.
        /// </summary>
        /// <param name="searchClient">The search client used to perform the search.</param>
        /// <param name="openAIClient">The OpenAIClient used to generate embeddings for the query.</param>
        /// <param name="query">The query to search for.</param>
        /// <param name="k">The number of nearest neighbors to retrieve (default is 3).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal static async Task SingleVectorSearch(SearchClient searchClient, OpenAIClient openAIClient, string query, int k = 3)
        {
            // Generate the embedding for the query
            var queryEmbeddings = await GenerateEmbeddings(query, openAIClient);

            // Debug: Output the query embeddings
            Console.WriteLine("Query Embeddings:");
            Console.WriteLine(string.Join(", ", queryEmbeddings));

            // Perform the vector similarity search
            var searchOptions = new SearchOptions
            {
                VectorQueries = { new RawVectorQuery() { Vector = queryEmbeddings.ToArray(), KNearestNeighborsCount = k, Fields = { "contentVector" } } },
                Size = k,
                Select = { "title", "content", "category" },
            };

            // Debug: Output the search options
            Console.WriteLine("Search Options:");
            Console.WriteLine($"VectorQueries: {JsonSerializer.Serialize(searchOptions.VectorQueries)}");
            Console.WriteLine($"Size: {searchOptions.Size}");
            Console.WriteLine($"Select: {string.Join(", ", searchOptions.Select)}");

            SearchResults<SearchDocument> response = await searchClient.SearchAsync<SearchDocument>(null, searchOptions);

            int count = 0;
            await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
            {
                count++;

                // Debug: Output search results
                Console.WriteLine("----------------------------------------");
                Console.WriteLine($"Result {count} - Document: {result.Document}");
                Console.WriteLine($"Result {count} - Title: {result.Document["title"]}");
                Console.WriteLine($"Result {count} - Score: {result.Score}\n");
                Console.WriteLine($"Result {count} - Content: {result.Document["content"]}");
                Console.WriteLine($"Result {count} - Category: {result.Document["category"]}\n");
            }

            Console.WriteLine($"Total Results: {count}");
        }


        /// <summary>
        /// Performs a single vector search with a filter on the search client using the provided query and filter.
        /// </summary>
        /// <param name="searchClient">The SearchClient instance used for the search.</param>
        /// <param name="openAIClient">The OpenAIClient instance used for generating query embeddings.</param>
        /// <param name="query">The search query or prompt.</param>
        /// <param name="filter">The filter to apply to the search.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal static async Task SingleVectorSearchWithFilter(SearchClient searchClient, OpenAIClient openAIClient, string query, string filter)
        {
            // Generate the embedding for the query
            var queryEmbeddings = await GenerateEmbeddings(query, openAIClient);

            // Perform the vector similarity search
            var searchOptions = new SearchOptions
            {
                VectorQueries = { new RawVectorQuery() { Vector = queryEmbeddings.ToArray(), KNearestNeighborsCount = 3, Fields = { "contentVector" } } },
                Filter = filter,
                Select = { "title", "content", "category" },
            };

            SearchResults<SearchDocument> response = await searchClient.SearchAsync<SearchDocument>(null, searchOptions);

            int count = 0;
            await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
            {
                count++;
                Console.WriteLine($"Title: {result.Document["title"]}");
                Console.WriteLine($"Score: {result.Score}\n");
                Console.WriteLine($"Content: {result.Document["content"]}");
                Console.WriteLine($"Category: {result.Document["category"]}\n");
            }
            Console.WriteLine($"Total Results: {count}");
        }

        /// <summary>
        /// Performs a simple hybrid search on the search client using the provided query.
        /// </summary>
        /// <param name="searchClient">The SearchClient instance used for the search.</param>
        /// <param name="openAIClient">The OpenAIClient instance used for generating query embeddings.</param>
        /// <param name="query">The search query or prompt.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal static async Task SimpleHybridSearch(SearchClient searchClient, OpenAIClient openAIClient, string query)
        {
            // Generate the embedding for the query
            var queryEmbeddings = await GenerateEmbeddings(query, openAIClient);

            // Perform the vector similarity search
            var searchOptions = new SearchOptions
            {
                VectorQueries = { new RawVectorQuery() { Vector = queryEmbeddings.ToArray(), KNearestNeighborsCount = 3, Fields = { "contentVector" } } },
                Size = 10,
                Select = { "title", "content", "category" },
            };

            SearchResults<SearchDocument> response = await searchClient.SearchAsync<SearchDocument>(query, searchOptions);

            int count = 0;
            await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
            {
                count++;
                Console.WriteLine($"Title: {result.Document["title"]}");
                Console.WriteLine($"Score: {result.Score}\n");
                Console.WriteLine($"Content: {result.Document["content"]}");
                Console.WriteLine($"Category: {result.Document["category"]}\n");
            }
            Console.WriteLine($"Total Results: {count}");
        }



        /// <summary>
        /// Performs a semantic hybrid search on the search client using the provided query.
        /// </summary>
        /// <param name="searchClient">The SearchClient instance used for the search.</param>
        /// <param name="openAIClient">The OpenAIClient instance used for generating query embeddings.</param>
        /// <param name="query">The search query or prompt.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        internal static async Task SemanticHybridSearch(SearchClient searchClient, OpenAIClient openAIClient, string query)
        {
            try
            {
                // Generate the embedding for the query
                var queryEmbeddings = await GenerateEmbeddings(query, openAIClient);

                // Perform the vector similarity search
                var searchOptions = new SearchOptions
                {
                    VectorQueries = { new RawVectorQuery() { Vector = queryEmbeddings.ToArray(), KNearestNeighborsCount = 3, Fields = { "contentVector" } } },
                    Size = 3,
                    QueryType = SearchQueryType.Semantic,
                    QueryLanguage = QueryLanguage.EnUs,
                    SemanticConfigurationName = SemanticSearchConfigName,
                    QueryCaption = QueryCaptionType.Extractive,
                    QueryAnswer = QueryAnswerType.Extractive,
                    QueryCaptionHighlightEnabled = true,
                    Select = { "title", "content", "category" },
                };

                SearchResults<SearchDocument> response = await searchClient.SearchAsync<SearchDocument>(query, searchOptions);

                int count = 0;
                Console.WriteLine("Semantic Hybrid Search Results:\n");

                Console.WriteLine("Query Answer:");
                foreach (AnswerResult result in response.Answers)
                {
                    Console.WriteLine($"Answer Highlights: {result.Highlights}");
                    Console.WriteLine($"Answer Text: {result.Text}\n");
                }

                await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
                {
                    count++;
                    Console.WriteLine($"Title: {result.Document["title"]}");
                    Console.WriteLine($"Reranker Score: {result.RerankerScore}");
                    Console.WriteLine($"Score: {result.Score}");
                    Console.WriteLine($"Content: {result.Document["content"]}");
                    Console.WriteLine($"Category: {result.Document["category"]}\n");

                    if (result.Captions != null)
                    {
                        var caption = result.Captions.FirstOrDefault();
                        if (caption != null)
                        {
                            if (!string.IsNullOrEmpty(caption.Highlights))
                            {
                                Console.WriteLine($"Caption Highlights: {caption.Highlights}\n");
                            }
                            else
                            {
                                Console.WriteLine($"Caption Text: {caption.Text}\n");
                            }
                        }
                    }
                }
                Console.WriteLine($"Total Results: {count}");
            }
            catch (NullReferenceException)
            {
                Console.WriteLine("Total Results: 0");
            }
        }





        /// <summary>
        /// Creates a sample search index with the given name and predefined configuration settings.
        /// </summary>
        /// <param name="name">The name of the search index.</param>
        /// <returns>A SearchIndex object representing the sample search index.</returns>
        internal static SearchIndex GetSampleIndex(string name)
        {
            // Define the names for the vector search profile and HNSW vector search configuration
            string vectorSearchProfile = "my-vector-profile";
            string vectorSearchHnswConfig = "my-hnsw-vector-config";

            // Create a new SearchIndex object with the specified name
            SearchIndex searchIndex = new(name)
            {
                // Configure vector search with the vector search profile and HNSW vector search algorithm
                VectorSearch = new()
                {
                    Profiles = { new VectorSearchProfile(vectorSearchProfile, vectorSearchHnswConfig) },
                    Algorithms = { new HnswVectorSearchAlgorithmConfiguration(vectorSearchHnswConfig) }
                },
                // Configure semantic search with a semantic configuration for title, content, and category fields
                SemanticSettings = new()
                {
                    Configurations =
            {
                new SemanticConfiguration(SemanticSearchConfigName, new()
                {
                    TitleField = new() { FieldName = "title" },
                    ContentFields = { new() { FieldName = "content" } },
                    KeywordFields = { new() { FieldName = "category" } }
                })
            }
                },
                // Define the fields of the search index, including ID, title, content, titleVector, contentVector, and category fields
                Fields =
        {
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
            new SearchableField("title") { IsFilterable = true, IsSortable = true },
            new SearchableField("content") { IsFilterable = true },
            new SearchField("titleVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
            {
                IsSearchable = true,
                VectorSearchDimensions = ModelDimensions,
                VectorSearchProfile = vectorSearchProfile
            },
            new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
            {
                IsSearchable = true,
                VectorSearchDimensions = ModelDimensions,
                VectorSearchProfile = vectorSearchProfile
            },
            new SearchableField("category") { IsFilterable = true, IsSortable = true, IsFacetable = true }
        }
            };

            // Return the created search index
            return searchIndex;
        }

        /// <summary>
        /// Retrieves a list of sample search documents asynchronously by generating embeddings for the input documents.
        /// </summary>
        /// <param name="openAIClient">The OpenAIClient used to generate embeddings.</param>
        /// <param name="inputDocuments">A list of input documents represented as dictionaries.</param>
        /// <returns>A task representing the asynchronous operation, returning a list of SearchDocument objects.</returns>
        internal static async Task<List<SearchDocument>> GetSampleDocumentsAsync(OpenAIClient openAIClient, List<Dictionary<string, object>> inputDocuments)
        {
            List<SearchDocument> sampleDocuments = new List<SearchDocument>();
            Console.WriteLine($"Processing {inputDocuments.Count} input documents...");

            foreach (var document in inputDocuments)
            {
                string title = document["title"]?.ToString() ?? string.Empty;
                string content = document["content"]?.ToString() ?? string.Empty;


               // Console.WriteLine($"Generating embeddings for title: {title}");
                float[] titleEmbeddings = (await GenerateEmbeddings(title, openAIClient)).ToArray();

                // Console.WriteLine($"Generating embeddings for content: {content}");
                float[] contentEmbeddings = (await GenerateEmbeddings(content, openAIClient)).ToArray();

                // Add the title embeddings to the document under the key "titleVector"
                document["titleVector"] = titleEmbeddings;

                // Add the content embeddings to the document under the key "contentVector"
                document["contentVector"] = contentEmbeddings;

                // Create a new SearchDocument with the document and add it to the sample documents
                sampleDocuments.Add(new SearchDocument(document));
            }
            // Print the number of documents for which embeddings were generated
            Console.WriteLine($"Generated embeddings for {sampleDocuments.Count} documents.");

            return sampleDocuments;
        }
    }
}
