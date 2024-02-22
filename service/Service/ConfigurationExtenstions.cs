using Amazon.SecretsManager;
using Amazon;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.KernelMemory;
using Microsoft.Extensions.Configuration.Memory;


public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddAWSSecretsManager(this IConfigurationBuilder configurationBuilder)
    {
        var client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName("us-west-2")); // Configure AWS client as needed

        var secretNames = new List<string>() { "dev/presalespals" }; // hard code for now

        foreach (var secretName in secretNames)
        {
            try
            {
                var secretValue = client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretName }).Result;
                if (secretValue != null)
                {
                    var secretString = secretValue.SecretString;
                    var secretData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(secretString);
                    
                    var flattenedData = FlattenDictionary(secretData);
                    configurationBuilder.AddInMemoryCollection(flattenedData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving secret {secretName}: {ex.Message}");
            }
        }

        return configurationBuilder;
    }

    private static IDictionary<string, string> FlattenDictionary(Dictionary<string, JsonElement> dict, string parentKey = "")
    {
        var result = new Dictionary<string, string>();

        foreach (var item in dict)
        {
            var key = string.IsNullOrEmpty(parentKey) ? item.Key : $"{parentKey}:{item.Key}";

            if(key == "KernelMemory")
            {
                var jsonString = "{\"Service\": {\"RunWebService\": true, \"RunHandlers\": true, \"OpenApiEnabled\": true}, \"ContentStorageType\": \"SimpleFileStorage\", \"TextGeneratorType\": \"AzureOpenAIText\", \"ServiceAuthorization\": {\"Enabled\": false, \"AuthenticationType\": \"APIKey\", \"HttpHeaderName\": \"Authorization\", \"AccessKey1\": \"\", \"AccessKey2\": \"\"}, \"DataIngestion\": {\"OrchestrationType\": \"Distributed\", \"DistributedOrchestration\": {\"QueueType\": \"SimpleQueues\"}, \"EmbeddingGenerationEnabled\": true, \"EmbeddingGeneratorTypes\": [\"AzureOpenAIEmbedding\"], \"MemoryDbTypes\": [\"Qdrant\"], \"ImageOcrType\": \"None\", \"TextPartitioning\": {\"MaxTokensPerParagraph\": 1000, \"MaxTokensPerLine\": 300, \"OverlappingTokens\": 100}, \"DefaultSteps\": []}, \"Retrieval\": {\"MemoryDbType\": \"Qdrant\", \"EmbeddingGeneratorType\": \"AzureOpenAIEmbedding\", \"SearchClient\": {\"MaxAskPromptSize\": -1, \"MaxMatchesCount\": 100, \"AnswerTokens\": 300, \"EmptyAnswer\": \"INFO NOT FOUND\"}}, \"Services\": {\"SimpleQueues\": {\"Directory\": \"_tmp_queues\"}, \"SimpleFileStorage\": {\"Directory\": \"_tmp_files\"}, \"AzureOpenAIEmbedding\": {\"APIType\": \"EmbeddingGeneration\", \"Auth\": \"ApiKey\", \"Endpoint\": \"https://tops-13576.openai.azure.com/\", \"Deployment\": \"tops-13650-text-embedding-ada-002\", \"APIKey\": \"a93c89f72ce24237b64eec15f7fba036\"}, \"Qdrant\": {\"Endpoint\": \"http://127.0.0.1:6333\", \"APIKey\": \"\"}, \"AzureOpenAIText\": {\"APIType\": \"ChatCompletion\", \"Auth\": \"ApiKey\", \"Endpoint\": \"https://tops-13730.openai.azure.com/\", \"Deployment\": \"tops-13730-gpt-4\", \"APIKey\": \"4e3af50bd3e34867b1baac9c73f23b11\", \"MaxRetries\": 10}}}";
                Dictionary<string, string> flattened = JsonFlattener.FlattenJson(item.Value.GetString());

                foreach (var kvp in flattened)
                {
                    //Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                    result.Add(kvp.Key,kvp.Value);
                }
                // JsonDocument doc = JsonDocument.Parse(item.Value.GetString());
                // var rootElement = doc.RootElement;
                // var dictionary = new Dictionary<string, JsonElement>();

                // foreach (var element in rootElement.EnumerateObject())
                // {
                //     dictionary[element.Name] = element.Value.Clone();
                //     result.Add(element.Name,element.Value.Clone().ToString());
                // }

            }
            if (item.Value.ValueKind == JsonValueKind.Object)
            {
                
                var nested = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(@item.Value.GetRawText());
                var nestedFlat = FlattenDictionary(nested, key);
                foreach (var nestedItem in nestedFlat)
                {
                    result.Add(nestedItem.Key, nestedItem.Value);
                }
            }
            else
            {
                result.Add(key, item.Value.ToString());
            }
        }

        return result;
    }

    // public async Task<ConfigurationRoot> LoadConfigurationFromSecretsManager(string secretValue)
    // {
    //     var configuration = JsonSerializer.Deserialize<ConfigurationRoot>(secretValue);
    //     return configuration;
    // }
    private static IEnumerable<KeyValuePair<string, string>> ConvertToKeyValuePairs(object obj, string parentKey = "")
    {
        var properties = obj.GetType().GetProperties();
        foreach (var property in properties)
        {
            var key = string.IsNullOrEmpty(parentKey) ? property.Name : $"{parentKey}:{property.Name}";
            var value = property.GetValue(obj);

            if (value != null && value.GetType().IsClass && value.GetType() != typeof(string))
            {
                foreach (var kvp in ConvertToKeyValuePairs(value, key))
                {
                    yield return kvp;
                }
            }
            else
            {
                yield return new KeyValuePair<string, string>(key, value?.ToString());
            }
        }
    }
    
}

public static class JsonFlattener
{
    public static Dictionary<string, string> FlattenJson(string jsonString)
    {
        var dictionary = new Dictionary<string, string>();
        using var doc = JsonDocument.Parse(jsonString);
        FlattenElement(doc.RootElement, dictionary, null);
        return dictionary;
    }

    private static void FlattenElement(JsonElement element, Dictionary<string, string> dict, string prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    FlattenElement(property.Value, dict, AppendPrefix(prefix, property.Name));
                }
                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenElement(item, dict, $"{prefix}[{index}]");
                    index++;
                }
                break;
            default:
                dict[prefix] = element.ToString();
                break;
        }
    }

    private static string AppendPrefix(string prefix, string name)
    {
        return string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";
    }
}