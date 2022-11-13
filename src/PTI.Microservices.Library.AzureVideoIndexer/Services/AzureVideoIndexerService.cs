using Microsoft.Extensions.Logging;
using PTI.Microservices.Library.AzureVideoIndexer.Models.CreateProject;
using PTI.Microservices.Library.Configuration;
using PTI.Microservices.Library.CustomExceptions;
using PTI.Microservices.Library.Interceptors;
using PTI.Microservices.Library.Models.AzureVideoIndexerService;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.CreateCustomFaces;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.CreatePerson;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.CreatePersonModel;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.GetAllPersonModels;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.GetAllVideos;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.GetCustomFaces;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.GetFacesArtifactInfo;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.GetPersonModels;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.GetPersonsInModel;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.GetVideoIndex;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.GetVideoStreamingUrl;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.SearchVideos;
using PTI.Microservices.Library.Models.AzureVideoIndexerService.UploadVideo;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace PTI.Microservices.Library.Services
{
    /// <summary>
    /// Service in charge of exposing access to Azure Video Indeer functionality
    /// </summary>
    public sealed class AzureVideoIndexerService
    {
        private ILogger<AzureVideoIndexerService> Logger { get; }
        private AzureVideoIndexerConfiguration AzureVideoIndexerConfiguration { get; }
        private CustomHttpClient CustomHttpClient { get; }
        public string AccountId => this.AzureVideoIndexerConfiguration.AccountId;
        public string Location => this.AzureVideoIndexerConfiguration.Location;

        /// <summary>
        /// Creates a new instance of <see cref="AzureVideoIndexerService"/>
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="azureVideoIndexerConfiguration"></param>
        /// <param name="customHttpClient"></param>
        public AzureVideoIndexerService(ILogger<AzureVideoIndexerService> logger,
            AzureVideoIndexerConfiguration azureVideoIndexerConfiguration, CustomHttpClient customHttpClient)
        {
            this.Logger = logger;
            this.AzureVideoIndexerConfiguration = azureVideoIndexerConfiguration;
            this.CustomHttpClient = customHttpClient;
            this.CustomHttpClient.Timeout = TimeSpan.FromDays(1);
            this.CustomHttpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key",
                this.AzureVideoIndexerConfiguration.Key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="personModelId"></param>
        /// <param name="personId"></param>
        /// <param name="imageUrl"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>A string representing the Custom Face Id</returns>
        public async Task<string> CreateCustomFaceAsync(Guid personModelId, Guid personId, List<Uri> imageUrl,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (imageUrl.Count > 20)
                    throw new Exception("Maximum of 20 items allowed per request");
                if (imageUrl.Any(p => p.Scheme.ToUpper() != "HTTPS"))
                    throw new Exception("Only HTTPS urls are allowed");
                foreach (var singleImage in imageUrl)
                {
                    if (!await IsValidImage(singleImage))
                        throw new Exception($"Supported picture size is from 36x36 to 4096x4096 pixels. Invalid Image: {singleImage}");
                }
                //"Invalid picture size: 3840 x 5760 pixels. Supported picture size is from 36x36 to 4096x4096 pixels."}
                //List<Uri> accessibleImages = new List<Uri>();
                var accountAccessToken = await this.GetAccountAccessTokenStringAsync(allowEdit: true);
                string requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Customization/PersonModels" +
                    $"/{personModelId}/Persons/{personId}/Faces" +
                    $"?accessToken={accountAccessToken}";
                CreateCustomFacesRequest model = new CreateCustomFacesRequest()
                {
                    Urls = imageUrl.Select(p => p.ToString()).ToArray()
                };
                var response = await this.CustomHttpClient.PostAsJsonAsync<CreateCustomFacesRequest>(requestUrl, model, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var parsedValue = JsonDocument.Parse(responseString);
                    var enumerator = parsedValue.RootElement.EnumerateArray();
                    var customFaceId = enumerator.First().GetString();
                    return customFaceId;

                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var details = await response.Content.ReadAsStringAsync();
                    var retryAfter = response.Headers.RetryAfter?.Delta;
                    string message = $"Error creating custom face. Person Model Id:{personModelId}. " +
                        $"Person Id: {personId}. Image Url:{imageUrl}. Reason: {reasonPhrase} - Details: {details}";
                    throw new AzureVideoIndexerCustomException(message, retryAfter);
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the thumnail for the specified video
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="thumbnailId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> GetVideoThumbnailAsync(string videoId, string thumbnailId, CancellationToken cancellationToken = default)
        {
            string videoAccessToken = await this.GetVideoAccessTokenStringAsync(videoId, true);
            string format = "Jpeg"; // Jpeg or Base64
            string requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                $"/Videos/{videoId}" +
                $"/Thumbnails/{thumbnailId}" +
                $"?format={format}" +
                $"&accessToken={videoAccessToken}";
            try
            {
                var imageBytes = await this.CustomHttpClient.GetByteArrayAsync(requestUrl);
                var base64ImageString = Convert.ToBase64String(imageBytes);
                return base64ImageString;
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Validates if the given url is a valid image
        /// </summary>
        /// <param name="singleImage"></param>
        /// <returns></returns>
        public async Task<bool> IsValidImage(Uri singleImage)
        {
            CustomHttpClient httpClient = new CustomHttpClient(new CustomHttpClientHandler(null));
            var singleImageStream = await httpClient.GetStreamAsync(singleImage);
            var imageInfo = System.Drawing.Bitmap.FromStream(singleImageStream);
            if (imageInfo.Width < 36 || imageInfo.Height < 36 || imageInfo.Width > 4096 || imageInfo.Height > 4096)
                return false;
            else
                return true;
        }

        /// <summary>
        /// Gets all Person Models
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<GetAllPersonModelsInfo[]> GetAllPersonModelsAsync(CancellationToken cancellationToken = default)
        {
            var accountAccessToken = await this.GetAccountAccessTokenStringAsync(allowEdit: true);
            string requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                $"/Customization" +
                $"/PersonModels" +
                //$"[?personNamePrefix]" +
                $"?accessToken={accountAccessToken}";
            try
            {
                var response = await this.CustomHttpClient.GetAsync(requestUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GetAllPersonModelsInfo[]>();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the sprite for a Custom Face
        /// </summary>
        /// <param name="personModelId"></param>
        /// <param name="personId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<byte[]> GetCustomFacesSpriteAsync(Guid personModelId, Guid personId, CancellationToken cancellationToken = default)
        {
            var accountAccessToken = await this.GetAccountAccessTokenStringAsync(allowEdit: true);
            string requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                $"/Customization/PersonModels/{personModelId}" +
                $"/Persons/{personId}" +
                $"/Faces/sprite" +
                //$"[?pageSize]" +
                //$"[&skip]" +
                //$"[&sourceType]" +
                $"?accessToken={accountAccessToken}";
            try
            {
                var response = await this.CustomHttpClient.GetAsync(requestUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsByteArrayAsync();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }

            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the picture for a Custom Face
        /// </summary>
        /// <param name="personModelId"></param>
        /// <param name="personId"></param>
        /// <param name="faceId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<byte[]> GetCustomFacePictureAsync(Guid personModelId, Guid personId, Guid faceId, CancellationToken cancellationToken = default)
        {
            var accountAccessToken = await this.GetAccountAccessTokenStringAsync(allowEdit: true);
            string requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                $"/Customization/PersonModels/{personModelId}" +
                $"/Persons/{personId}" +
                $"/Faces/{faceId}" +
                $"?accessToken={accountAccessToken}";
            try
            {
                var response = await this.CustomHttpClient.GetAsync(requestUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsByteArrayAsync();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }

            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the Custom Faces
        /// </summary>
        /// <param name="personModelId"></param>
        /// <param name="personId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<GetCustomFacesResponse> GetCustomFacesAsync(Guid personModelId, Guid personId, CancellationToken cancellationToken = default)
        {
            var accountAccessToken = await this.GetAccountAccessTokenStringAsync(allowEdit: true);
            string requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                $"/Customization/PersonModels/{personModelId}" +
                $"/Persons/{personId}" +
                $"/Faces" +
                //$"[?pageSize]" +
                //$"[&skip]" +
                //$"[&sourceType]" +
                $"?accessToken={accountAccessToken}";
            try
            {
                var response = await this.CustomHttpClient.GetAsync(requestUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GetCustomFacesResponse>();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }

            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets All Persons in Model
        /// </summary>
        /// <param name="personModelId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<GetPersonsInModelResponse> GetPersonsInModelAsync(Guid personModelId, CancellationToken cancellationToken = default)
        {
            var accountAccessToken = await this.GetAccountAccessTokenStringAsync(allowEdit: true);
            string requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                $"/Customization" +
                $"/PersonModels/{personModelId}" +
                $"/Persons" +
                //$"[?namePrefix]" +
                //$"[&pageSize]" +
                //$"[&skip]" +
                $"?accessToken={accountAccessToken}";
            try
            {
                var response = await this.CustomHttpClient.GetAsync(requestUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GetPersonsInModelResponse>();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }

            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets a Person Model with the specified name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<GetPersonModelsInfo[]> GetPersonModelByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            var accountAccessToken = await this.GetAccountAccessTokenStringAsync(allowEdit: true);
            var requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                $"/Customization/" +
                $"PersonModels" +
                //$"?personNamePrefix={name}" +
                $"?accessToken={accountAccessToken}";
            try
            {
                var response = await this.CustomHttpClient.GetAsync(requestUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GetPersonModelsInfo[]>();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }

            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Creates a Person
        /// </summary>
        /// <param name="personModelId"></param>
        /// <param name="name"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<CreatePersonResponse> CreatePersonAsync(Guid personModelId, string name, CancellationToken cancellationToken = default)
        {
            try
            {
                var accountAccessToken = await this.GetAccountAccessTokenStringAsync(allowEdit: true);
                string requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Customization" +
                    $"/PersonModels/{personModelId}" +
                    $"/Persons" +
                    $"?name={name}" +
                    $"&accessToken={accountAccessToken}";
                var response = await this.CustomHttpClient.PostAsync(requestUrl, null, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<CreatePersonResponse>();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Creates a Person Model
        /// </summary>
        /// <param name="name"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<CreatePersonModelResponse> CreatePersonModelAsync(string name, CancellationToken cancellationToken = default)
        {
            try
            {
                string accountAccessToken = await this.GetAccountAccessTokenStringAsync(allowEdit: true);
                string requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Customization/PersonModels" +
                    $"?name={name}" +
                    $"&accessToken={accountAccessToken}";
                var response = await this.CustomHttpClient.PostAsync(requestUrl, null, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<CreatePersonModelResponse>();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, ex.Message);
                throw;
            }
        }

        private async Task<string> GetAccountAccessTokenStringAsync(bool allowEdit)
        {
            string requestUrl = $"{this.AzureVideoIndexerConfiguration.BaseAPIUrl}" +
                $"Auth/{this.AzureVideoIndexerConfiguration.Location}" +
                $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                $"/AccessToken" +
                $"?allowEdit={allowEdit}";
            var result = await this.CustomHttpClient.GetStringAsync(requestUrl);
            return result.Replace("\"", "");
        }

        /// <summary>
        /// Gets the video access token
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="allowEdit"></param>
        /// <returns></returns>
        public async Task<string> GetVideoAccessTokenStringAsync(string videoId, bool allowEdit)
        {
            string requestUrl = $"{this.AzureVideoIndexerConfiguration.BaseAPIUrl}" +
                $"/Auth/{this.AzureVideoIndexerConfiguration.Location}" +
                $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                $"/Videos/{videoId}/AccessToken" +
                $"?allowEdit={allowEdit}";
            var result = await this.CustomHttpClient.GetStringAsync(requestUrl);
            return result.Replace("\"", string.Empty);
        }

        /// <summary>
        /// Gets the index for the specified video
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<GetVideoIndexResponse> GetVideoIndexAsync(string videoId, CancellationToken cancellationToken = default)
        {
            try
            {
                string videoAccessToken = await this.GetVideoAccessTokenStringAsync(videoId, true);
                string requestUrl = $"https://api.videoindexer.ai" +
                    $"/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos/{videoId}" +
                    $"/Index" +
                    $"?accessToken={videoAccessToken}";
                string jsonResponse = await this.CustomHttpClient.GetStringAsync(requestUrl, cancellationToken);
                var videoIndexResult = await this.CustomHttpClient.GetFromJsonAsync<GetVideoIndexResponse>(requestUrl,
                    cancellationToken);
                return videoIndexResult;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Gets all videos
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<GetAllVideosResponse> GetAllVideosAsync(CancellationToken cancellationToken = default)
        {
            var accountAccessToken =
                await this.GetAccountAccessTokenStringAsync(false);
            string requestUrl = $"{this.AzureVideoIndexerConfiguration.BaseAPIUrl}" +
                $"/{this.AzureVideoIndexerConfiguration.Location}" +
                $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                $"/Videos?accessToken={accountAccessToken}";
            var result = await this.CustomHttpClient.GetFromJsonAsync<GetAllVideosResponse>(requestUrl, cancellationToken);
            return result;
        }

        /// <summary>
        /// Gets the keywords for the specified video
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<KeywordInfoModel>> GetVideoKeywordsAsync(string videoId, CancellationToken cancellationToken = default)
        {
            List<KeywordInfoModel> lstKeywords = new List<KeywordInfoModel>();
            var singleVideoIndex = await this.GetVideoIndexAsync(videoId, cancellationToken);
            if (singleVideoIndex.summarizedInsights.keywords.Count() > 0)
            {
                foreach (var singleKeyword in singleVideoIndex.summarizedInsights.keywords)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var existentKeyWordInfo = lstKeywords.Where(p => p.Keyword == singleKeyword.name).SingleOrDefault();
                    if (existentKeyWordInfo != null)
                    {
                        existentKeyWordInfo.Appeareances += singleKeyword.appearances.Count();
                    }
                    else
                    {
                        existentKeyWordInfo = new KeywordInfoModel()
                        {
                            Keyword = singleKeyword.name,
                            Appeareances = singleKeyword.appearances.Count()
                        };
                        lstKeywords.Add(existentKeyWordInfo);
                    }
                }
            }
            return lstKeywords.Distinct().ToList();
        }

        /// <summary>
        /// Gets all keywords across all videos
        /// </summary>
        /// <param name="onNewKeywordFound"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<KeywordInfoModel>> GetAllKeywordsAsync(Action<string> onNewKeywordFound = null, CancellationToken cancellationToken = default)
        {
            List<KeywordInfoModel> lstKeywords = new List<KeywordInfoModel>();
            var allVideos = await this.GetAllVideosAsync(cancellationToken);
            foreach (var singleVideo in allVideos.results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var singleVideoIndex = await this.GetVideoIndexAsync(singleVideo.id, cancellationToken);
                if (singleVideoIndex.summarizedInsights.keywords.Count() > 0)
                {
                    foreach (var singleKeyword in singleVideoIndex.summarizedInsights.keywords)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var existentKeyWordInfo = lstKeywords.Where(p => p.Keyword == singleKeyword.name).SingleOrDefault();
                        if (existentKeyWordInfo != null)
                        {
                            existentKeyWordInfo.Appeareances += singleKeyword.appearances.Count();
                        }
                        else
                        {
                            existentKeyWordInfo = new KeywordInfoModel()
                            {
                                Keyword = singleKeyword.name,
                                Appeareances = singleKeyword.appearances.Count()
                            };
                            lstKeywords.Add(existentKeyWordInfo);
                            if (onNewKeywordFound != null)
                                onNewKeywordFound(singleKeyword.name);
                        }
                    }
                }
            }
            return lstKeywords.Distinct().ToList();
        }

        /// <summary>
        /// Video Privacy
        /// </summary>
        public enum VideoPrivacy
        {
            /// <summary>
            /// Public
            /// </summary>
            Public,
            /// <summary>
            /// Private
            /// </summary>
            Private
        }


        /// <summary>
        /// Gets the information of faces inclusing their positions in frames
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<GetFacesArtifactInfoResponse> GetFacesArtifactInfoAsync(string videoId, CancellationToken cancellationToken = default)
        {
            try
            {
                var artifactDownloadUrl = await this.GetVideoArtifactDownloadUrlAsync(videoId, ArtifactType.Faces, cancellationToken);
                artifactDownloadUrl = artifactDownloadUrl.TrimStart('\"').TrimEnd('\"');
                var facesJson = await this.CustomHttpClient.GetFromJsonAsync<GetFacesArtifactInfoResponse>(artifactDownloadUrl);
                return facesJson;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }
        /// <summary>
        /// Uploads and Indexes a given video from a specified Url
        /// </summary>
        /// <param name="videoBase64String"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="fileName"></param>
        /// <param name="personModelId"></param>
        /// <param name="privacy"></param>
        /// <param name="callBackUri">The url to receive notification after video has been idnexed.
        /// See more here: https://api-portal.videoindexer.ai/docs/services/Operations/operations/Upload-Video?
        /// </param>
        /// <param name="metadata"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<UploadVideoResponse> UploadVideoFromBase64StringAsync(String videoBase64String, string name,
            string description, string fileName,
            Guid personModelId, VideoPrivacy privacy,
            Uri callBackUri,
            List<VideoMetadataAttribute> metadata = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var accountAccessToken = await this.GetAccountAccessTokenStringAsync(true);
                string requestUrl =
                    $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos" +
                    $"?name={name}" +
                    $"&privacy={privacy}" +
                    //$"[&priority]" +
                    $"&description={description}";
                if (metadata != null && metadata.Count > 0)
                {
                    requestUrl += $"&metadata={HttpUtility.UrlEncode(BuildMetadataString(metadata))}";

                }
                requestUrl +=
                //$"[&partition]" +
                //$"[&externalId]" +
                //$"[&externalUrl]" +
                $"&callbackUrl={HttpUtility.UrlEncode(callBackUri.ToString())}" +
                //$"[&language]" +
                //$"&videoUrl={videoUri}" +
                $"&fileName={fileName}" +
                //$"[&indexingPreset]" +
                //$"[&streamingPreset]" +
                //$"[&linguisticModelId]" +
                $"&personModelId={personModelId}" +
                //$"[&animationModelId]" +
                $"&sendSuccessEmail={true}" +
                //$"[&assetId]" +
                //$"[&brandsCategories]" +
                $"&accessToken={accountAccessToken}";
                MultipartFormDataContent multipartContent =
                    new MultipartFormDataContent();
                var bytes = Convert.FromBase64String(videoBase64String);
                multipartContent.Add(
                    new StreamContent(new MemoryStream(bytes)),
                    "file", fileName);
                var response = await this.CustomHttpClient.PostAsync(requestUrl, multipartContent, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UploadVideoResponse>();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Uploads and Indexes a given video from a specified Url.
        /// Check Api docs here:
        /// https://api-portal.videoindexer.ai/api-details#api=Operations&operation=Upload-Video
        /// </summary>
        /// <param name="videoUri"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="fileName"></param>
        /// <param name="personModelId"></param>
        /// <param name="privacy"></param>
        /// <param name="callBackUri">The url to receive notification after video has been idnexed.
        /// See more here: https://api-portal.videoindexer.ai/api-details#api=Operations&operation=Upload-Video
        /// </param>
        /// <param name="language">Check supported languages here: 
        /// https://api-portal.videoindexer.ai/api-details#api=Operations&operation=Upload-Video</param>
        /// <param name="indexingPreset">Allowed values: Default / AudioOnly / VideoOnly / BasicAudio / Advanced / AdvancedAudio / AdvancedVide</param>
        /// <param name="metadata"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<UploadVideoResponse> UploadVideoAsync(Uri videoUri, string name,
            string description, string fileName,
            Guid personModelId, VideoPrivacy privacy,
            Uri callBackUri,
            string language = "auto",
            string indexingPreset = "Default",
            List<VideoMetadataAttribute> metadata = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var accountAccessToken = await this.GetAccountAccessTokenStringAsync(true);
                string requestUrl =
                    $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos" +
                    $"?name={name}" +
                    $"&privacy={privacy}" +
                    //$"[&priority]" +
                    $"&description={HttpUtility.UrlEncode(description)}";
                if (metadata != null && metadata.Count > 0)
                {
                    requestUrl += $"&metadata={HttpUtility.UrlEncode(BuildMetadataString(metadata))}";

                }
                requestUrl +=
                //$"[&partition]" +
                //$"[&externalId]" +
                //$"[&externalUrl]" +
                $"&callbackUrl={HttpUtility.UrlEncode(callBackUri.ToString())}" +
                $"&language={language}" +
                $"&videoUrl={HttpUtility.UrlEncode(videoUri.ToString())}" +
                $"&fileName={HttpUtility.UrlEncode(fileName)}" +
                $"&indexingPreset={indexingPreset}";
                //$"[&streamingPreset]" +
                //$"[&linguisticModelId]" +
                if (personModelId != Guid.Empty)
                {
                    requestUrl += $"&personModelId={personModelId}";
                }
                requestUrl +=
                //$"[&animationModelId]" +
                $"&sendSuccessEmail={true}" +
                //$"[&assetId]" +
                //$"[&brandsCategories]" +
                $"&accessToken={accountAccessToken}";
                var response = await this.CustomHttpClient.PostAsync(requestUrl, null, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<UploadVideoResponse>();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }


        private string BuildMetadataString(List<VideoMetadataAttribute> metadata)
        {
            var result = System.Text.Json.JsonSerializer.Serialize(metadata);
            return result;
        }

        /// <summary>
        /// Returns the HTML code for the Video Insights Website
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="allowEdit"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> GetVideoInsightsWidgetAsync(string videoId, bool allowEdit,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var videoAccessToken = await this.GetVideoAccessTokenStringAsync(videoId, true);
                string requestUrl = $"https://api.videoindexer.ai/" +
                    $"{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos/{videoId}/" +
                    $"InsightsWidget" +
                    //$"[?widgetType]" +
                    $"?allowEdit={allowEdit}" +
                    $"&accessToken={videoAccessToken}";
                var response = await this.CustomHttpClient.GetAsync(requestUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Returns the HTML code for the Video Player Website
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> GetVideoPlayerWidgetAsync(string videoId, CancellationToken cancellationToken = default)
        {
            try
            {
                var accountAccessToken = await this.GetVideoAccessTokenStringAsync(videoId, true);
                string requestUrl = $"https://api.videoindexer.ai" +
                    $"/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos/{videoId}" +
                    $"/PlayerWidget" +
                    $"?accessToken={accountAccessToken}";
                var response = await this.CustomHttpClient.GetAsync(requestUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Artifact Type
        /// </summary>
        public enum ArtifactType
        {
            /// <summary>
            /// Ocr
            /// </summary>
            Ocr,
            /// <summary>
            /// Faces
            /// </summary>
            Faces,
            /// <summary>
            /// Faces Thumbnails
            /// </summary>
            FacesThumbnails,
            /// <summary>
            /// Visual Content Moderation
            /// </summary>
            VisualContentModeration,
            /// <summary>
            /// Keyframes Thumbnails
            /// </summary>
            KeyframesThumbnails,
            /// <summary>
            /// Language Detection
            /// </summary>
            LanguageDetection,
            //MultiLanguageDetection,
            /// <summary>
            /// Metadata
            /// </summary>
            Metadata,
            /// <summary>
            /// Emotions
            /// </summary>
            Emotions,
            /// <summary>
            /// Textual Content Moderation
            /// </summary>
            TextualContentModeration,
            //AudioEffects
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="artifactType"> The artifact type to get. Allowed values: Ocr/Faces/FacesThumbnails/VisualContentModeration/KeyframesThumbnails/LanguageDetection/MultiLanguageDetection/Metadata/Emotions/TextualContentModeration/AudioEffects</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> GetVideoArtifactDownloadUrlAsync(string videoId,
            ArtifactType artifactType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string videoAccessToken = await this.GetVideoAccessTokenStringAsync(videoId, true);
                string requestUrl = $"https://api.videoindexer.ai" +
                    $"/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos/{videoId}" +
                    $"/ArtifactUrl" +
                    $"?type={artifactType}" +
                    $"&accessToken={videoAccessToken}";
                string result = await this.CustomHttpClient.GetStringAsync(requestUrl, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Downloads all artifacts to the specified output stream
        /// </summary>
        /// <param name="outputStream"></param>
        /// <param name="videoId"></param>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        public async Task DownloadAllArtifactsAsync(Stream outputStream, string videoId, CancellationToken cancellation = default)
        {
            try
            {
                using (ZipArchive zipArchive = new ZipArchive(outputStream, ZipArchiveMode.Create, true))
                {

                    var allNames = Enum.GetNames(typeof(ArtifactType));
                    foreach (var singleArtifactName in allNames)
                    {
                        try
                        {
                            var downloadLink = await this.GetVideoArtifactDownloadUrlAsync(videoId, Enum.Parse<ArtifactType>(singleArtifactName),
                                cancellation);
                            Uri requestUrl = new Uri(downloadLink.Trim('\"'));
                            var fileBytes = await this.CustomHttpClient.GetByteArrayAsync(requestUrl, cancellation);
                            string fileName = string.Empty;
                            switch (Enum.Parse<ArtifactType>(singleArtifactName))
                            {
                                case ArtifactType.FacesThumbnails:
                                case ArtifactType.KeyframesThumbnails:
                                    fileName = $"{singleArtifactName}.zip";
                                    break;
                                default:
                                    fileName = $"{singleArtifactName}.json";
                                    break;
                            }
                            var zipEntry = zipArchive.CreateEntry(fileName);
                            var zipEntryStream = zipEntry.Open();
                            await zipEntryStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                            zipEntryStream.Close();
                        }
                        catch (Exception ex)
                        {
                            this.Logger?.LogError($"Unable process artifact {singleArtifactName} for video :{videoId}. {ex.Message}", ex);
                        }
                    }
                }
                outputStream.Position = 0;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Specified where to search
        /// </summary>
        public enum SearchScope
        {
            /// <summary>
            /// Transcript
            /// </summary>
            Transcript,
            /// <summary>
            /// Topics
            /// </summary>
            Topics,
            /// <summary>
            /// Ocr
            /// </summary>
            Ocr,
            /// <summary>
            /// Annotations
            /// </summary>
            Annotations,
            /// <summary>
            /// Brands
            /// </summary>
            Brands,
            /// <summary>
            /// Names Locations
            /// </summary>
            NamedLocations,
            /// <summary>
            /// Names People
            /// </summary>
            NamedPeople
        }

        /// <summary>
        /// Deletes the specified video
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeleteVideoAsync(string videoId, CancellationToken cancellationToken = default)
        {
            try
            {
                string accountAccessToken = await this.GetAccountAccessTokenStringAsync(true);
                string requestUrl = $"https://api.videoindexer.ai" +
                    $"/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts" +
                    $"/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos/{videoId}" +
                    $"?accessToken={accountAccessToken}";
                var response = await this.CustomHttpClient.DeleteAsync(requestUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Search for videos using the specified search term
        /// </summary>
        /// <param name="videoIds"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<SearchVideosResponse> SearchVideosByIdsAsync(string[] videoIds,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var idKeyPairs = videoIds.Select(p => $"id={p}");
                string idQueryString = String.Join("&", idKeyPairs);
                string accountAccessToken = await this.GetAccountAccessTokenStringAsync(true);
                string requestUrl = $"{this.AzureVideoIndexerConfiguration.BaseAPIUrl}" +
                    $"/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos/Search" +
                    //$"[&isBase]" +
                    //$"[&hasSourceVideoFile]" +
                    //$"[&sourceVideoId]" +
                    //$"[&state]" +
                    //$"[&privacy]" +
                    //$"[&id]" +
                    //$"[&partition]" +
                    //$"[&externalId]" +
                    //$"[&owner]" +
                    //$"[&face]" +
                    //$"[&animatedcharacter]" +
                    $"?{idQueryString}";
                //$"[&textScope]" +
                //if (!string.IsNullOrWhiteSpace(sourceLanguage))
                //{
                //    requestUrl += $"&sourceLanguage={sourceLanguage}";
                //}
                //if (!String.IsNullOrWhiteSpace(language))
                //{
                //    requestUrl += $"&language={language}";
                //}
                requestUrl +=
                //$"[&createdAfter]" +
                //$"[&createdBefore]" +
                //$"[&pageSize]" +
                //$"[&skip]" +
                $"&accessToken={accountAccessToken}";
                var result = await this.CustomHttpClient.GetFromJsonAsync<SearchVideosResponse>(requestUrl, cancellationToken: cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Search for videos using the specified search term
        /// </summary>
        /// <param name="searchTerm"></param>
        /// <param name="searchScopes"></param>
        /// <param name="sourceLanguage">Restrict the search to videos on the specified language. 
        /// If not set, videos in all languages will be retrieved. For more information check
        /// https://api-portal.videoindexer.ai/docs/services/Operations/operations/Search-Videos?
        /// </param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<SearchVideosResponse> SearchVideosAsync(string searchTerm, SearchScope[] searchScopes,
            string sourceLanguage = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string accountAccessToken = await this.GetAccountAccessTokenStringAsync(true);
                string requestUrl = $"{this.AzureVideoIndexerConfiguration.BaseAPIUrl}" +
                    $"/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos/Search" +
                    //$"[&isBase]" +
                    //$"[&hasSourceVideoFile]" +
                    //$"[&sourceVideoId]" +
                    //$"[&state]" +
                    //$"[&privacy]" +
                    //$"[&id]" +
                    //$"[&partition]" +
                    //$"[&externalId]" +
                    //$"[&owner]" +
                    //$"[&face]" +
                    //$"[&animatedcharacter]" +
                    $"?query={searchTerm}";
                //$"[&textScope]" +
                if (!string.IsNullOrWhiteSpace(sourceLanguage))
                {
                    requestUrl += $"&sourceLanguage={sourceLanguage}";
                }
                //if (!String.IsNullOrWhiteSpace(language))
                //{
                //    requestUrl += $"&language={language}";
                //}
                requestUrl +=
                //$"[&createdAfter]" +
                //$"[&createdBefore]" +
                //$"[&pageSize]" +
                //$"[&skip]" +
                $"&accessToken={accountAccessToken}";
                var result = await this.CustomHttpClient.GetFromJsonAsync<SearchVideosResponse>(requestUrl, cancellationToken: cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the streaming url for the specified video
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="language"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<GetVideoStreamingUrlResponse> GetVideoStreamingUrlAsync(string videoId,
            string language = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string videoAccessToken = await this.GetVideoAccessTokenStringAsync(videoId, true);
                string requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos/{videoId}" +
                    $"/streaming-url" +
                    //$"[?useProxy]" +
                    //$"[&urlFormat]" +
                    //$"[&tokenLifetimeInMinutes]" +
                    $"?accessToken={videoAccessToken}";
                var result = await this.CustomHttpClient.GetFromJsonAsync<GetVideoStreamingUrlResponse>(requestUrl, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        /// <summary>
        /// Retrieve the source video download url for the specified video id
        /// </summary>
        /// <param name="videoId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<Uri> GetVideoSourceFileDownloadUrlAsync(string videoId, CancellationToken cancellationToken = default)
        {
            try
            {
                string videoAccessToken = await this.GetVideoAccessTokenStringAsync(videoId, true);
                string requestUrl = $"https://api.videoindexer.ai/{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos/{videoId}" +
                    $"/SourceFile/DownloadUrl" +
                    $"?accessToken={videoAccessToken}";
                var result = await this.CustomHttpClient.GetStringAsync(requestUrl);
                result = result.TrimStart('\"').TrimEnd('\"');
                return new Uri(result);
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        public enum CaptionsFormat
        {
            Vtt,
            Ttml,
            Srt,
            Txt,
            Csv
        }

        public async Task<byte[]> GetVideoCaptionsAsync(string videoId, CaptionsFormat captionsFormat,
            bool includeAudioEffects = true, CancellationToken cancellationToken = default)
        {
            try
            {
                string videoAccessToken = await this.GetVideoAccessTokenStringAsync(videoId, true);
                string requestUrl = $"https://api.videoindexer.ai/" +
                    $"{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Videos/{videoId}" +
                    $"/Captions" +
                    //$"[?indexId]" +
                    $"?format={captionsFormat}" +
                    //$"[&language]" +
                    $"&includeAudioEffects={includeAudioEffects}" +
                    $"&accessToken={videoAccessToken}";
                var result = await this.CustomHttpClient.GetByteArrayAsync(requestUrl);
                return result;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        public async Task<CreateProjectResponse> CreateProjectAsync(CreateProjectRequest model, CancellationToken cancellationToken = default)
        {
            try
            {
                string accountAccessToken = await this.GetAccountAccessTokenStringAsync(true);
                string requestUrl = $"https://api.videoindexer.ai/" +
                    $"{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Projects" +
                    $"?accessToken={accountAccessToken}";
                var response = await this.CustomHttpClient.PostAsJsonAsync<CreateProjectRequest>(requestUrl, model);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<CreateProjectResponse>();
                    return result;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }

        public async Task RenderProjectAsync(string projectId, CancellationToken cancellation = default)
        {
            try
            {
                string accountAccessToken = await this.GetAccountAccessTokenStringAsync(true);
                string requestUrl = $"https://api.videoindexer.ai/" +
                    $"{this.AzureVideoIndexerConfiguration.Location}" +
                    $"/Accounts/{this.AzureVideoIndexerConfiguration.AccountId}" +
                    $"/Projects/{projectId}" +
                    $"/render" +
                    $"?sendCompletionEmail={true}" +
                    $"&accessToken={accountAccessToken}";
                var response = await this.CustomHttpClient.PostAsync(requestUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
                else
                {
                    var reasonPhrase = response.ReasonPhrase;
                    var responseContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error: {reasonPhrase} - Details:{responseContent}");
                }
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex.Message, ex);
                throw;
            }
        }
    }
}
