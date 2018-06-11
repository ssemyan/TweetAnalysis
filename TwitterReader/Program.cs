using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Tweetinvi;
using Tweetinvi.Models;

namespace TwitterReader
{
	class Program
	{
		static void Main(string[] args)
		{
			//Configure Twitter OAuth
			var oauthToken = ConfigurationManager.AppSettings["oauth_token"];
			var oauthTokenSecret = ConfigurationManager.AppSettings["oauth_token_secret"];
			var oauthCustomerKey = ConfigurationManager.AppSettings["oauth_consumer_key"];
			var oauthConsumerSecret = ConfigurationManager.AppSettings["oauth_consumer_secret"];
			var keywords = ConfigurationManager.AppSettings["twitter_keywords"];

			// https://github.com/linvi/tweetinvi/wiki/Streams
			Auth.SetUserCredentials(oauthCustomerKey, oauthConsumerSecret, oauthToken, oauthTokenSecret);

			var stream = Stream.CreateFilteredStream();
			stream.AddTrack("beer");
			stream.AddTweetLanguageFilter(LanguageFilter.English);

			// Create client for CA
			ITextAnalyticsAPI client = new TextAnalyticsAPI(new ApiKeyServiceClientCredentials())
			{
				AzureRegion = AzureRegions.Westeurope
			};

			stream.MatchingTweetReceived += (sender, targs) =>
			{
				var tweet = targs.Tweet;
				Console.WriteLine("New {0} Beer Tweet: {1}", tweet.Language, tweet.Text);

				// Get Language
				var result = client.DetectLanguageAsync(new BatchInput(new List<Input> { new Input(tweet.Id.ToString(), tweet.Text) })).Result;
				foreach (var document in result.Documents)
				{
					Console.WriteLine("Document ID: {0} , Language: {1}", document.Id, document.DetectedLanguages[0].Name);

					// Get Phrases
					var inputTweet = new List<MultiLanguageInput>() { new MultiLanguageInput(document.DetectedLanguages[0].Iso6391Name, document.Id, tweet.Text) };
					KeyPhraseBatchResult result2 = client.KeyPhrasesAsync(new MultiLanguageBatchInput(inputTweet)).Result;

					foreach (var document2 in result2.Documents)
					{
						Console.WriteLine("Document ID: {0} ", document2.Id);
						Console.WriteLine("\t Key phrases:");
						foreach (string keyphrase in document2.KeyPhrases)
						{
							Console.WriteLine("\t\t" + keyphrase);
						}
					}

					// Get Sentiment
					SentimentBatchResult result3 = client.SentimentAsync(new MultiLanguageBatchInput(inputTweet)).Result;
					foreach (var document3 in result3.Documents)
					{
						Console.WriteLine("Document ID: {0} , Sentiment Score: {1:0.00}", document3.Id, document3.Score);
					}

					// Get Named Entities
					var result4 = client.EntitiesAsync(new MultiLanguageBatchInput(inputTweet)).Result;
					foreach(var document4 in result4.Documents)
					{
						Console.WriteLine("Entities found:");
						foreach (var entity in document4.Entities)
						{
							Console.WriteLine("Entity Name: {0}", entity.Name);
						}						
					}
				}
			};

			stream.StartStreamMatchingAllConditions();
		}
	}

	class ApiKeyServiceClientCredentials : ServiceClientCredentials
	{
		public override Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var apiToken = ConfigurationManager.AppSettings["cognitive_service_key"];
			request.Headers.Add("Ocp-Apim-Subscription-Key", apiToken);
			return base.ProcessHttpRequestAsync(request, cancellationToken);
		}
	}
}
