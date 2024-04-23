# Azure OpenAI ChatGPT and Vector AI Search Demo

This project demonstrates how to generate personalized chat responses using Azure OpenAI's Chat GPT model on your own data. It's a console application written in C#, that uses the OpenAI API to interact with the Chat GPT model.

The application takes a chat query from the user and uses the `GetChatCompletionsAsync` method from the OpenAI Chat GPT model to generate a response. This feature allows the application to use the Chat GPT model on your own data, enabling more personalized and context-specific responses.

More details on OpenAI ChatGPT model: https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/chatgpt?tabs=python&pivots=programming-language-chat-completions

Please follow the instructions in the [Getting Started](#getting-started) section to set up and run the project on your local machine.

## Getting Started

These instructions will get you a copy of the project up and running on your local machine for development and testing purposes.

### Prerequisites

- .NET 7.0
- Azure OpenAI service
- Azure Search Documents service
- AI Models
   - gpt-35-turbo chat model
   - text-embedding-ada-002 mode 

### Configuration


Open the `local.settings.json` file and replace the placeholders with yours

Here's an example of what the file might look like:

```json
    "AZURE_SEARCH_SERVICE_ENDPOINT": "",
    "AZURE_SEARCH_INDEX_NAME": "",
    "AZURE_SEARCH_ADMIN_KEY": "",
    "AZURE_OPENAI_ENDPOINT": "",
    "AZURE_OPENAI_API_KEY": "",
    "AZURE_OPENAI_API_VERSION": "2023-05-15",
    "AZURE_OPENAI_EMBEDDING_DEPLOYED_MODEL": "text-embedding-ada-002"
```

### Installation

1. Clone the repository to your local machine.
2. Navigate to the project directory.
3. Run the following command to start the application:

```bash
dotnet run
```

### Sample

This console application demonstrates the integration of the OpenAI Chat GPT model using the `GetChatCompletionsAsync` method. It provides a unique feature of utilizing the Chat GPT model on user-specific data, enabling more personalized and context-aware responses.
<img width="1339" alt="chat" src="https://github.com/edward232232/Chatgpt-Own-Data-App/assets/62457821/cda07e3f-7743-4177-bf87-db2ffca61ba9">
