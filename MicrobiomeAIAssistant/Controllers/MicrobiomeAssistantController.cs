using Microsoft.AspNetCore.Mvc;
using Azure.AI.OpenAI;
using Azure;
using System.Text;

namespace MicrobiomeAIAssistant.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MicrobiomeAssistantController : ControllerBase
    {

        private readonly ILogger<MicrobiomeAssistantController> _logger;

        public MicrobiomeAssistantController(ILogger<MicrobiomeAssistantController> logger)
        {
            _logger = logger;
        }

        [HttpPost(Name = "PromptMicrobiomeAssistant")]
        public async Task<string> Post([FromBody] string inputPrompt)
        {
            // Azure OpenAI setup
            var apiBase = "https://microbiome-academic-gpt-assistant.openai.azure.com/"; // Add your endpoint here
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"); // Add your OpenAI API key here
            var deploymentId = "gpt-35-turbo-16k"; // Add your deployment ID here

            // Azure AI Search setup
            var searchEndpoint = "https://academic-microbiome-cognitive-search.search.windows.net"; // Add your Azure AI Search endpoint here
            var searchKey = Environment.GetEnvironmentVariable("SEARCH_KEY"); // Add your Azure AI Search admin key here
            var searchIndexName = "microbiome-study-index"; // Add your Azure AI Search index name here
            var client = new OpenAIClient(new Uri(apiBase), new AzureKeyCredential(apiKey!));

            var azureCognitiveSearchConfiguration = new AzureCognitiveSearchChatExtensionConfiguration()
            {
                SearchEndpoint = new Uri(searchEndpoint),
                IndexName = searchIndexName
            };

            azureCognitiveSearchConfiguration.SetSearchKey(searchKey);

            var chatCompletionsOptions = new ChatCompletionsOptions()
            {
                DeploymentName = deploymentId,
                Messages =
                {
                    new ChatMessage(ChatRole.User, inputPrompt)
                },
                MaxTokens = 1000,
                Temperature = 0,

                // The addition of AzureChatExtensionsOptions enables the use of Azure OpenAI capabilities that add to
                // the behavior of Chat Completions, here the "using your own data" feature to supplement the context
                // with information from an Azure AI Search resource with documents that have been indexed.

                AzureExtensionsOptions = new AzureChatExtensionsOptions()
                {
                    Extensions = { azureCognitiveSearchConfiguration }
                }
            };

            var response = await client.GetChatCompletionsAsync(chatCompletionsOptions);

            var message = response.Value.Choices[0].Message;
            var references = new StringBuilder();
            // Responses that used extensions will also have Context information that includes special Tool messages
            // to explain extension activity and provide supplemental information like citations.

            foreach (var contextMessage in message.AzureExtensionsContext.Messages)
            {
                // Note: citations and other extension payloads from the "tool" role are often encoded JSON documents
                // and need to be parsed as such; that step is omitted here for brevity.
                references.Append($"{contextMessage.Role}: {contextMessage.Content}");
            }


            return $"{message.Role}: {message.Content} === {references} ===";
        }
    }
}