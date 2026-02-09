using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GoogleSpeechToText.Scripts
{
    public class GoogleCloudSpeechToText : MonoBehaviour
    {
        // The API endpoint (OAuth required)
        private const string apiEndpoint = "https://speech.googleapis.com/v1/speech:recognize";

        // Sends a request to Google Speech-to-Text API (OAuth2 access token required)
        public static void SendSpeechToTextRequest(byte[] bytes, int sampleRateHertz, string accessToken, Action<string> onSuccess, Action<BadRequestData> onError)
        {
            string base64Content = Convert.ToBase64String(bytes);

            

            var requestData = new SpeechToTextRequest
            {
                config = new SpeechConfig
                {
                    encoding = "LINEAR16",
                    sampleRateHertz = sampleRateHertz,
                    languageCode = "en-US",
                    enableWordTimeOffsets = false
                },
                audio = new AudioData
                {
                    // uri = audioUri
                    content = base64Content,
                }
            };

            // Set headers for the request
            var headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + accessToken },
                { "Content-Type", "application/json; charset=utf-8" }
            };

            // Use OAuth endpoint (no API key parameter)
            string url = apiEndpoint;

            // Serialize request data to JSON
            string requestJson = JsonUtility.ToJson(requestData);

            // Call the Post method to send the request
            Post(url, requestJson, onSuccess, onError, headers);
        }

        private static async void Post(string url, string bodyJsonString, Action<string> onSuccess, Action<BadRequestData> onError, Dictionary<string, string> headers)
        {
            var request = new UnityWebRequest(url, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 30;

            // Add headers to the request
            foreach (var header in headers)
            {
                request.SetRequestHeader(header.Key, header.Value);
            }

            var operation = request.SendWebRequest();

            // Wait for the request to complete
            while (!operation.isDone)
                await Task.Yield();

            // Check for errors
            if (HasError(request, out var badRequest))
            {
                onError?.Invoke(badRequest);
            }
            else
            {
                onSuccess?.Invoke(request.downloadHandler.text);
            }

            request.Dispose();
        }

        private static bool HasError(UnityWebRequest request, out BadRequestData badRequestData)
        {
            if (request.responseCode == 200 || request.responseCode == 201)
            {
                badRequestData = null;
                return false;
            }

            var rawText = request.downloadHandler != null ? request.downloadHandler.text : null;
            string resultInfo = request.result.ToString();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                badRequestData = new BadRequestData
                {
                    error = new Error
                    {
                        code = (int)request.responseCode,
                        message = string.IsNullOrEmpty(request.error)
                            ? $"Empty error response body. Result={resultInfo}"
                            : $"{request.error} (Result={resultInfo})"
                    }
                };
                return true;
            }

            try
            {
                badRequestData = JsonUtility.FromJson<BadRequestData>(rawText);
                if (badRequestData == null || badRequestData.error == null)
                {
                    badRequestData = new BadRequestData
                    {
                        error = new Error
                        {
                            code = (int)request.responseCode,
                            message = rawText
                        }
                    };
                }
                return true;
            }
            catch (Exception)
            {
                badRequestData = new BadRequestData
                {
                    error = new Error
                    {
                        code = (int)request.responseCode,
                        message = string.IsNullOrEmpty(request.error)
                            ? $"{rawText} (Result={resultInfo})"
                            : $"{request.error} (Result={resultInfo})"
                    }
                };
                return true;
            }
        }
    }

    // The request data format for Google Speech-to-Text API
    [System.Serializable]
    public class SpeechToTextRequest
    {
        public SpeechConfig config;
        public AudioData audio;
    }

    [System.Serializable]
    public class SpeechConfig
    {
        public string encoding;
        public int sampleRateHertz;
        public string languageCode;
        public bool enableWordTimeOffsets;
    }

    [System.Serializable]
    public class AudioData
    {
        // public string uri;
        public string  content;
    }

    // Response format for Google Speech-to-Text API
    [System.Serializable]
    public class SpeechToTextResponse
    {
        public Result[] results;
    }

    [System.Serializable]
    public class Result
    {
        public Alternative[] alternatives;
    }

    [System.Serializable]
    public class Alternative
    {
        public string transcript;
        public float confidence;
    }

    // Error response format
    [System.Serializable]
    public class BadRequestData
    {
        public Error error;
    }

    [System.Serializable]
    public class Error
    {
        public int code;
        public string message;
    }
}
