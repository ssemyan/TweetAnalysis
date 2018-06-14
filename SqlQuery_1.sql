drop table if exists TweetInfo
go
create table TweetInfo(
        TweetId int identity(1,1) primary key,
		TweetCreatedAt smalldatetime,
		TweetText nvarchar(1000), 
		TweetAuthor nvarchar(255),
		TweetAuthorLocation nvarchar(255),
		TweetAuthorFollowerCount int, 
		TweetAuthorCreatedAt smalldatetime,
		TweetGeoFullName nvarchar(255),
		TweetGeoCountryCode nvarchar(5),
		TweetRetweetCount int,
		TweetFavoriteCount int,
		TweetHashtags nvarchar(512),
		TweetUrl nvarchar(255),
		TweetMediaUrls nvarchar(512),
		TweetLanguageCode nvarchar(5),
		TweetSentiment float,
		TweetKeyPhrases nvarchar(1024),
		TweetNamedEntities nvarchar(1024)	
)
