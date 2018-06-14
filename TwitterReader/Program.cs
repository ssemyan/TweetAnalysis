using Microsoft.Azure.CognitiveServices.Language.TextAnalytics;
using Microsoft.Azure.CognitiveServices.Language.TextAnalytics.Models;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
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

			// Create a stream for the requested keyword
			var stream = Stream.CreateFilteredStream();
			var tracks = keywords.Split(',');

			foreach (var keyword in tracks)
			{
				stream.AddTrack(keyword);
			}

			// Create client for CA
			ITextAnalyticsAPI client = new TextAnalyticsAPI(new ApiKeyServiceClientCredentials())
			{
				AzureRegion = AzureRegions.Westeurope
			};

			stream.MatchingTweetReceived += (sender, targs) =>
			{
				var tweet = targs.Tweet;
				Console.WriteLine("New Tweet: {0}", tweet.Text);

				var tweetRow = new TweetWithAnalysis()
				{
					TweetAuthor = tweet.CreatedBy.Name,
					TweetAuthorCreatedAt = tweet.CreatedBy.CreatedAt,
					TweetAuthorFollowerCount = tweet.CreatedBy.FollowersCount,
					TweetAuthorLocation = tweet.CreatedBy.Location ?? "Unknown",
					TweetCreatedAt = tweet.CreatedAt,
					TweetFavoriteCount = tweet.FavoriteCount,
					TweetGeoCountryCode = (tweet.Place == null) ? "" : tweet.Place.CountryCode,
					TweetGeoFullName = (tweet.Place == null) ? "Unknown" : tweet.Place.FullName,
					TweetRetweetCount = tweet.RetweetCount,
					TweetText = tweet.Text,
					TweetUrl = tweet.Url,
					TweetNamedEntities = ""
			};

				List<string> entityHashtags = new List<string>();
				foreach (var entity in tweet.Entities.Hashtags)
				{
					entityHashtags.Add(entity.Text);
				}
				tweetRow.TweetHashtags = string.Join(", ", entityHashtags);

				List<string> mediaUrls = new List<string>();
				foreach (var media in tweet.Media)
				{
					mediaUrls.Add(media.MediaURL);
				}
				tweetRow.TweetMediaUrls = string.Join(", ", mediaUrls);

				// Get Language
				var result = client.DetectLanguageAsync(new BatchInput(new List<Input> { new Input(tweet.Id.ToString(), tweet.Text) })).Result;
				foreach (var document in result.Documents)
				{
					Console.WriteLine("Document ID: {0} , Language: {1}", document.Id, document.DetectedLanguages[0].Name);
					tweetRow.TweetLanguageCode = document.DetectedLanguages[0].Iso6391Name;

					// Get Phrases
					var inputTweet = new List<MultiLanguageInput>() { new MultiLanguageInput(document.DetectedLanguages[0].Iso6391Name, document.Id, tweet.Text) };
					KeyPhraseBatchResult result2 = client.KeyPhrasesAsync(new MultiLanguageBatchInput(inputTweet)).Result;

					foreach (var document2 in result2.Documents)
					{
						string keyPhrases = string.Join(", ", document2.KeyPhrases);
						Console.WriteLine("Key phrases: {0}", keyPhrases);
						tweetRow.TweetKeyPhrases = keyPhrases;
					}

					// Get Sentiment
					SentimentBatchResult result3 = client.SentimentAsync(new MultiLanguageBatchInput(inputTweet)).Result;
					foreach (var document3 in result3.Documents)
					{
						Console.WriteLine("Sentiment Score: {0:0.00}", document3.Score);
						tweetRow.TweetSentiment = document3.Score;
					}

					// Get Named Entities
					var result4 = client.EntitiesAsync(new MultiLanguageBatchInput(inputTweet)).Result;
					foreach (var document4 in result4.Documents)
					{
						List<string> entityNames = new List<string>();
						foreach (var entity in document4.Entities)
						{
							entityNames.Add(entity.Name);
						}

						string entities = string.Join(", ", entityNames);
						Console.WriteLine("Entities: {0}", entities);
						tweetRow.TweetNamedEntities = entities;
						Console.WriteLine("Entities found: {0}", entities);
					}
				}

				Console.WriteLine(tweetRow.ToJson());

				string conn = "Data Source=localhost;Initial Catalog=WorldCup;Integrated Security=True";
				using (SqlConnection connection = new SqlConnection(conn))
				{
					string sqlCommand = "INSERT into TweetInfo (TweetCreatedAt, TweetText, TweetAuthor, " +
											"TweetAuthorLocation, TweetAuthorFollowerCount, TweetAuthorCreatedAt," +
											"TweetGeoFullName, TweetGeoCountryCode, TweetRetweetCount, " +
											"TweetFavoriteCount, TweetHashtags, TweetUrl, TweetMediaUrls, " +
											"TweetLanguageCode, TweetSentiment, TweetKeyPhrases, TweetNamedEntities)" +
											" VALUES (@TweetCreatedAt, @TweetText, @TweetAuthor, " +
											"@TweetAuthorLocation, @TweetAuthorFollowerCount, @TweetAuthorCreatedAt," +
											"@TweetGeoFullName, @TweetGeoCountryCode, @TweetRetweetCount, " +
											"@TweetFavoriteCount, @TweetHashtags, @TweetUrl, @TweetMediaUrls, " +
											"@TweetLanguageCode, @TweetSentiment, @TweetKeyPhrases, @TweetNamedEntities)";

					using (SqlCommand command = new SqlCommand(sqlCommand))
					{
						command.Connection = connection;
						command.Parameters.AddWithValue("@TweetCreatedAt", tweetRow.TweetCreatedAt);
						command.Parameters.AddWithValue("@TweetText", tweetRow.TweetText);
						command.Parameters.AddWithValue("@TweetAuthor", tweetRow.TweetAuthor);
						command.Parameters.AddWithValue("@TweetAuthorLocation", tweetRow.TweetAuthorLocation);
						command.Parameters.AddWithValue("@TweetAuthorFollowerCount", tweetRow.TweetAuthorFollowerCount);
						command.Parameters.AddWithValue("@TweetAuthorCreatedAt", tweetRow.TweetAuthorCreatedAt);
						command.Parameters.AddWithValue("@TweetGeoFullName", tweetRow.TweetGeoFullName);
						command.Parameters.AddWithValue("@TweetGeoCountryCode", tweetRow.TweetGeoCountryCode);
						command.Parameters.AddWithValue("@TweetRetweetCount", tweetRow.TweetRetweetCount);
						command.Parameters.AddWithValue("@TweetFavoriteCount", tweetRow.TweetFavoriteCount);
						command.Parameters.AddWithValue("@TweetHashtags", tweetRow.TweetHashtags);
						command.Parameters.AddWithValue("@TweetUrl", tweetRow.TweetUrl);
						command.Parameters.AddWithValue("@TweetMediaUrls", tweetRow.TweetMediaUrls);
						command.Parameters.AddWithValue("@TweetLanguageCode", tweetRow.TweetLanguageCode);
						command.Parameters.AddWithValue("@TweetSentiment", tweetRow.TweetSentiment);
						command.Parameters.AddWithValue("@TweetKeyPhrases", tweetRow.TweetKeyPhrases);
						command.Parameters.AddWithValue("@TweetNamedEntities", tweetRow.TweetNamedEntities);

						connection.Open();
						int recordsAffected = command.ExecuteNonQuery();
					}

				};

			};

			// Start the stream but only if all tracks are found
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

	[Serializable]
	class TweetWithAnalysis
	{
		public DateTime TweetCreatedAt;
		public string TweetText;
		public string TweetAuthor;
		public string TweetAuthorLocation;
		public int TweetAuthorFollowerCount;
		public DateTime TweetAuthorCreatedAt;
		public string TweetGeoFullName;
		public string TweetGeoCountryCode;
		public int TweetRetweetCount;
		public int TweetFavoriteCount;
		public string TweetHashtags;
		public string TweetUrl;
		public string TweetMediaUrls;
		public string TweetLanguageCode;
		public double? TweetSentiment;
		public string TweetKeyPhrases;
		public string TweetNamedEntities;		
	}
}
