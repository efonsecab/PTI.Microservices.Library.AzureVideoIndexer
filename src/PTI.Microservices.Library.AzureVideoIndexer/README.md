# PTI.Microservices.Library.AzureVideoIndexer

Facilitates the consumption of the APIs in Azure Video Analyzer For Media ( formerly Azure Video Indexer )

**Examples:**

**Note: The examples below are passing null for the logger, if you want to use the logger make sure to pass the parameter with a value other than null**

## Search Videos
    AzureVideoIndexerService azureVideoIndexerService =
       new AzureVideoIndexerService(null, this.AzureVideoIndexerConfiguration, new CustomHttpClient(new CustomHttpClientHandler(null)));
    var videos = await azureVideoIndexerService.SearchVideosAsync("", null, sourceLanguage:"es-ES");
	
## Get Video Keywords
    string videoId = "REPLACE WITH YOUR VIDEO ID";
    AzureVideoIndexerService azureVideoIndexerService =
       new AzureVideoIndexerService(null, this.AzureVideoIndexerConfiguration, new CustomHttpClient(new CustomHttpClientHandler(null)));
    var videos = await azureVideoIndexerService.GetVideoKeywordsAsync(videoId)