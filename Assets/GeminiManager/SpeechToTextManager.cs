using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using UnityEngine.InputSystem;

namespace GoogleSpeechToText.Scripts
{
    public class SpeechToTextManager : MonoBehaviour
    {
        // [SerializeField] private string audioUri = "gs://cloud-samples-tests/speech/brooklyn.flac"; // Audio file URI in Google Cloud Storage
        [Header("Google Cloud Speech-to-Text OAuth")]
        [Tooltip("Optional: drag the service-account JSON asset here (recommended).")]
        [SerializeField] private TextAsset serviceAccountJson;
        [Tooltip("Path to service-account JSON. Use an absolute path or place it in StreamingAssets and enter the filename.")]
        [SerializeField] private string serviceAccountJsonPath;
        [SerializeField] private string oauthScope = "https://www.googleapis.com/auth/cloud-platform";
        [Header("Gemini Manager Prefab")]
        public UnityAndGeminiV3 geminiManager;

        [Header("Recording")]
        [Tooltip("If true, press and hold Space to record. If false, recording starts automatically.")]
        [SerializeField] private bool pushToTalk = false;
        [Tooltip("RMS threshold above which speech is considered present.")]
        [SerializeField] private float silenceThreshold = 0.01f;
        [Tooltip("Seconds of silence before auto-stopping and sending.")]
        [SerializeField] private float silenceDuration = 0.8f;
        [Tooltip("If true, stop recording after silence; if false, only stop at max duration.")]
        [SerializeField] private bool stopOnSilence = false;
        [Tooltip("If true, end recording shortly after speech ends (faster responses).")]
        [SerializeField] private bool endAfterSpeech = true;
        [Tooltip("Silence (seconds) after speech before ending recording.")]
        [SerializeField] private float endAfterSpeechSilenceSeconds = 0.4f;
        [Tooltip("Maximum seconds to record before auto-stopping.")]
        [SerializeField] private float maxRecordSeconds = 4f;
        [Tooltip("Seconds to wait with no speech before restarting the recording buffer.")]
        [SerializeField] private float noSpeechRestartSeconds = 1.0f;
        [Tooltip("Microphone sample rate for recording.")]
        [SerializeField] private int recordingSampleRate = 16000;
        [Tooltip("Number of samples to analyze for voice activity.")]
        [SerializeField] private int sampleWindow = 1024;
        [Tooltip("If true, mutes global audio while recording to prevent echo.")]
        [SerializeField] private bool muteGlobalAudioWhileRecording = false;
                
        private AudioClip clip;
        private byte[] bytes;
        private bool recording = false;
        private float previousAudioVolume = 1f;
        private float recordingStartTime;
        private float lastVoiceTime;
        private bool hasVoiceInCurrentClip;

        private string accessToken;
        private long accessTokenExpiryUnix;

        [Serializable]
        private class ServiceAccountKey
        {
            public string type;
            public string project_id;
            public string private_key_id;
            public string private_key;
            public string client_email;
            public string client_id;
            public string token_uri;
        }

        [Serializable]
        private class OAuthTokenResponse
        {
            public string access_token;
            public int expires_in;
            public string token_type;
        }

        private void Awake()
        {
            Debug.Log("SpeechToTextManager: Awake on " + gameObject.name);
        }

        private void OnEnable()
        {
            Debug.Log("SpeechToTextManager: OnEnable on " + gameObject.name);
        }

        private void Start()
        {
            Debug.Log("SpeechToTextManager: Start on " + gameObject.name);
            // Pre-fetch OAuth token to surface errors early
            StartCoroutine(EnsureAccessToken());

            if (!pushToTalk)
            {
                StartRecording();
            }
        }

    void Update()
    {
         if (!recording)
         {
             // Lightweight heartbeat once per start to confirm Update is running
             if (Time.frameCount == 1)
             {
                 Debug.Log("SpeechToTextManager: Update running on " + gameObject.name);
             }
         }
         if (pushToTalk)
         {
             if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && !recording)
             {
                 StartRecording();
             }

             // Check if the spacebar is released
             if (Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame && recording)
             {
                 StopRecording();
             }
         }
         else
         {
             if (!recording)
             {
                 StartRecording();
             }

             if (recording)
             {
                 var level = GetCurrentMicLevel();
                 if (level > silenceThreshold)
                 {
                     lastVoiceTime = Time.time;
                     hasVoiceInCurrentClip = true;
                 }

                 if (stopOnSilence && hasVoiceInCurrentClip && Time.time - lastVoiceTime >= silenceDuration)
                 {
                     StopRecording();
                 }
                 else if (endAfterSpeech && hasVoiceInCurrentClip && Time.time - lastVoiceTime >= endAfterSpeechSilenceSeconds)
                 {
                     StopRecording();
                 }
                 else if (hasVoiceInCurrentClip && Time.time - recordingStartTime >= maxRecordSeconds)
                 {
                     StopRecording();
                 }
                 else if (!hasVoiceInCurrentClip && Time.time - recordingStartTime >= noSpeechRestartSeconds)
                 {
                     RestartRecording();
                 }
             }
         }
    }


    private void StartRecording()
    {
        // optionally mute global audio to avoid echo/playback of what's being recorded
        if (muteGlobalAudioWhileRecording)
        {
            previousAudioVolume = AudioListener.volume;
            AudioListener.volume = 0f;
        }

        int lengthSeconds = Mathf.Max(1, Mathf.CeilToInt(maxRecordSeconds));
        int sampleRate = Mathf.Clamp(recordingSampleRate, 8000, 48000);
        clip = Microphone.Start(null, false, lengthSeconds, sampleRate);
        recording = true;
        recordingStartTime = Time.time;
        lastVoiceTime = Time.time;
        hasVoiceInCurrentClip = false;
    }

    private byte[] EncodeAsWAV(float[] samples, int frequency, int channels) {
        using (var memoryStream = new MemoryStream(44 + samples.Length * 2)) {
            using (var writer = new BinaryWriter(memoryStream)) {
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + samples.Length * 2);
                writer.Write("WAVE".ToCharArray());
                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write((ushort)1);
                writer.Write((ushort)channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * 2);
                writer.Write((ushort)(channels * 2));
                writer.Write((ushort)16);
                writer.Write("data".ToCharArray());
                writer.Write(samples.Length * 2);

                foreach (var sample in samples) {
                    writer.Write((short)(sample * short.MaxValue));
                }
            }
            return memoryStream.ToArray();
        }
    }

    private void StopRecording()
    {
            var position = Microphone.GetPosition(null);
            Microphone.End(null);

            if (clip == null || position <= 0)
            {
                recording = false;
                if (muteGlobalAudioWhileRecording)
                {
                    AudioListener.volume = previousAudioVolume;
                }
                return;
            }

            int safePosition = Mathf.Min(position, clip.samples);
            int sampleCount = safePosition * clip.channels;
            if (sampleCount <= 0)
            {
                recording = false;
                if (muteGlobalAudioWhileRecording)
                {
                    AudioListener.volume = previousAudioVolume;
                }
                return;
            }

            var samples = new float[sampleCount];
            clip.GetData(samples, 0);
            bytes = EncodeAsWAV(samples, clip.frequency, clip.channels);
            recording = false;

            // Stop and clear any AudioSource that may be playing the recorded clip to prevent accidental playback
            if (clip != null)
            {
                var allAudioSources = FindObjectsOfType<AudioSource>();
                foreach (var src in allAudioSources)
                {
                    if (src != null && src.clip == clip)
                    {
                        src.Stop();
                        src.clip = null;
                    }
                }
            }

            // clear local reference to the recorded clip
            clip = null;

            // restore audio volume now that recording has stopped
            if (muteGlobalAudioWhileRecording)
            {
                AudioListener.volume = previousAudioVolume;
            }

            if (!hasVoiceInCurrentClip)
            {
                return;
            }

            // If TTS is currently playing, ignore this recording to prevent feedback/echo
            if (ConversationState.IsPlayingTTS)
            {
                Debug.Log("SpeechToTextManager: TTS is playing; ignoring recorded audio to avoid feedback.");
                return;
            }

            StartCoroutine(SendSpeechWithOAuth(bytes));
    }

    private void RestartRecording()
    {
        Microphone.End(null);
        recording = false;
        clip = null;

        if (muteGlobalAudioWhileRecording)
        {
            AudioListener.volume = previousAudioVolume;
        }

        StartRecording();
    }

    private float GetCurrentMicLevel()
    {
        if (clip == null) return 0f;
        int micPosition = Microphone.GetPosition(null);
        if (micPosition <= 0) return 0f;

        int window = Mathf.Clamp(sampleWindow, 64, clip.samples);
        if (micPosition < window) return 0f;

        var samples = new float[window];
        clip.GetData(samples, micPosition - window);
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i];
            sum += s * s;
        }
        return Mathf.Sqrt(sum / samples.Length);
    }

    private IEnumerator SendSpeechWithOAuth(byte[] audioBytes)
    {
        if (audioBytes == null || audioBytes.Length == 0)
        {
            Debug.LogError("SpeechToTextManager: No audio bytes to send.");
            yield break;
        }

        yield return StartCoroutine(EnsureAccessToken());

        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("SpeechToTextManager: Access token is missing; cannot call STT.");
            yield break;
        }

        Debug.Log($"SpeechToTextManager: sending {audioBytes.Length} bytes to STT with OAuth token.");

        GoogleCloudSpeechToText.SendSpeechToTextRequest(audioBytes, clip != null ? clip.frequency : 16000, accessToken,
            (response) => {
                Debug.Log("Speech-to-Text Response: " + response);
                // Parse the response if needed
                var speechResponse = JsonUtility.FromJson<SpeechToTextResponse>(response);
                if (speechResponse?.results != null && speechResponse.results.Length > 0 && speechResponse.results[0].alternatives.Length > 0)
                {
                    var transcript = speechResponse.results[0].alternatives[0].transcript;
                    Debug.Log("Transcript: " + transcript);
                    if (geminiManager == null)
                    {
                        geminiManager = FindObjectOfType<UnityAndGeminiV3>();
                        if (geminiManager == null)
                        {
                            Debug.LogWarning("SpeechToTextManager: geminiManager is missing or destroyed; cannot send chat.");
                            return;
                        }
                    }
                    geminiManager.SendChat(transcript);
                }
                else
                {
                    Debug.LogWarning("SpeechToTextManager: No transcript found in response.");
                }

            },
            (error) => {
                if (error == null)
                {
                    Debug.LogError("STT Error: unknown (error object was null)");
                }
                else if (error.error == null)
                {
                    Debug.LogError("STT Error: " + JsonUtility.ToJson(error));
                }
                else
                {
                    Debug.LogError("STT Error: " + error.error.message + " (code " + error.error.code + ")");
                }
            });
    }

    private IEnumerator EnsureAccessToken()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!string.IsNullOrEmpty(accessToken) && now < accessTokenExpiryUnix - 60)
        {
            yield break; // token still valid
        }

        string json = null;

        if (serviceAccountJson != null)
        {
            Debug.Log("SpeechToTextManager: Using ServiceAccount TextAsset.");
            json = serviceAccountJson.text;
        }
        else
        {
            string path = ResolveServiceAccountPath(serviceAccountJsonPath);
            Debug.Log("SpeechToTextManager: Resolved service account path: " + path);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError("SpeechToTextManager: Service account JSON not found. Set TextAsset or path. Resolved path: " + path);
                yield break;
            }
            json = File.ReadAllText(path);
        }
        var key = JsonUtility.FromJson<ServiceAccountKey>(json);
        if (key == null || string.IsNullOrEmpty(key.private_key) || string.IsNullOrEmpty(key.client_email) || string.IsNullOrEmpty(key.token_uri))
        {
            Debug.LogError("SpeechToTextManager: Invalid service account JSON.");
            yield break;
        }

        Debug.Log("SpeechToTextManager: Using token_uri=" + key.token_uri + ", client_email=" + key.client_email);

        string jwt = CreateJwt(key, oauthScope);
        if (string.IsNullOrEmpty(jwt))
        {
            Debug.LogError("SpeechToTextManager: Failed to create JWT.");
            yield break;
        }

        using var www = new UnityEngine.Networking.UnityWebRequest(key.token_uri, "POST");
        string body = "grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer&assertion=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(jwt);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);
        www.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

        yield return www.SendWebRequest();

        if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Debug.LogError("SpeechToTextManager: OAuth token request failed: " + www.error + " - " + www.downloadHandler.text);
            yield break;
        }

        var token = JsonUtility.FromJson<OAuthTokenResponse>(www.downloadHandler.text);
        if (token == null || string.IsNullOrEmpty(token.access_token))
        {
            Debug.LogError("SpeechToTextManager: OAuth token response invalid: " + www.downloadHandler.text);
            yield break;
        }

        accessToken = token.access_token;
        accessTokenExpiryUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + token.expires_in;
        Debug.Log("SpeechToTextManager: OAuth token acquired.");
    }

    private string ResolveServiceAccountPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (Path.IsPathRooted(path)) return path;
        return Path.Combine(Application.streamingAssetsPath, path);
    }

    private string CreateJwt(ServiceAccountKey key, string scope)
    {
        try
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long exp = now + 3600;

            string headerJson = "{\"alg\":\"RS256\",\"typ\":\"JWT\"}";
            string payloadJson = "{\"iss\":\"" + JsonEscape(key.client_email) + "\",\"scope\":\"" + JsonEscape(scope) + "\",\"aud\":\"" + JsonEscape(key.token_uri) + "\",\"exp\":" + exp + ",\"iat\":" + now + "}";

            string header = Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(headerJson));
            string payload = Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(payloadJson));
            string unsignedToken = header + "." + payload;

            byte[] signature = SignWithServiceAccountKey(unsignedToken, key.private_key);
            if (signature == null) return null;

            string signed = Base64UrlEncode(signature);
            return unsignedToken + "." + signed;
        }
        catch (Exception e)
        {
            Debug.LogError("SpeechToTextManager: JWT creation failed: " + e);
            return null;
        }
    }

    private byte[] SignWithServiceAccountKey(string data, string privateKeyPem)
    {
        byte[] keyData = PemToBytes(privateKeyPem);
        if (keyData == null) return null;

        try
        {
            var rsaParams = Pkcs8ToRSAParameters(keyData);
            using var rsa = new System.Security.Cryptography.RSACryptoServiceProvider();
            rsa.ImportParameters(rsaParams);
            return rsa.SignData(System.Text.Encoding.UTF8.GetBytes(data), System.Security.Cryptography.CryptoConfig.MapNameToOID("SHA256"));
        }
        catch (Exception e)
        {
            Debug.LogError("SpeechToTextManager: RSA sign failed: " + e);
            return null;
        }
    }

    private byte[] PemToBytes(string pem)
    {
        if (string.IsNullOrEmpty(pem)) return null;
        const string header = "-----BEGIN PRIVATE KEY-----";
        const string footer = "-----END PRIVATE KEY-----";
        // Handle escaped newlines from JSON ("\\n") and normalize
        string normalized = pem.Replace("\\n", "\n");
        string trimmed = normalized.Replace(header, string.Empty).Replace(footer, string.Empty).Replace("\n", "").Replace("\r", "");
        try
        {
            return Convert.FromBase64String(trimmed);
        }
        catch (Exception e)
        {
            Debug.LogError("SpeechToTextManager: Failed to decode private key base64: " + e);
            return null;
        }
    }

    private string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private string JsonEscape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    // Minimal PKCS#8 parser to RSAParameters (avoids ImportPkcs8PrivateKey unsupported on some Unity platforms)
    private System.Security.Cryptography.RSAParameters Pkcs8ToRSAParameters(byte[] pkcs8)
    {
        var reader = new DerReader(pkcs8);
        reader.ReadSequence();
        reader.ReadInteger(); // version
        reader.ReadSequence(); // algorithm identifier
        reader.SkipValue(); // OID
        if (reader.PeekTag() == 0x05) reader.SkipValue(); // NULL
        byte[] privateKeyOctet = reader.ReadOctetString();

        var pkReader = new DerReader(privateKeyOctet);
        pkReader.ReadSequence();
        pkReader.ReadInteger(); // version

        var modulus = pkReader.ReadIntegerBytes();
        var publicExponent = pkReader.ReadIntegerBytes();
        var privateExponent = pkReader.ReadIntegerBytes();
        var prime1 = pkReader.ReadIntegerBytes();
        var prime2 = pkReader.ReadIntegerBytes();
        var exponent1 = pkReader.ReadIntegerBytes();
        var exponent2 = pkReader.ReadIntegerBytes();
        var coefficient = pkReader.ReadIntegerBytes();

        return new System.Security.Cryptography.RSAParameters
        {
            Modulus = modulus,
            Exponent = publicExponent,
            D = privateExponent,
            P = prime1,
            Q = prime2,
            DP = exponent1,
            DQ = exponent2,
            InverseQ = coefficient
        };
    }

    private class DerReader
    {
        private readonly byte[] data;
        private int pos;

        public DerReader(byte[] data)
        {
            this.data = data;
            pos = 0;
        }

        public byte PeekTag()
        {
            return data[pos];
        }

        public void ReadSequence()
        {
            ReadTag(0x30);
            ReadLength();
        }

        public void ReadInteger()
        {
            ReadTag(0x02);
            int len = ReadLength();
            pos += len;
        }

        public byte[] ReadIntegerBytes()
        {
            ReadTag(0x02);
            int len = ReadLength();
            byte[] val = new byte[len];
            Buffer.BlockCopy(data, pos, val, 0, len);
            pos += len;
            return TrimLeadingZero(val);
        }

        public byte[] ReadOctetString()
        {
            ReadTag(0x04);
            int len = ReadLength();
            byte[] val = new byte[len];
            Buffer.BlockCopy(data, pos, val, 0, len);
            pos += len;
            return val;
        }

        public void SkipValue()
        {
            byte tag = data[pos++];
            int len = ReadLength();
            pos += len;
        }

        private void ReadTag(byte expected)
        {
            byte tag = data[pos++];
            if (tag != expected)
            {
                throw new Exception("Unexpected DER tag: " + tag + " expected: " + expected);
            }
        }

        private int ReadLength()
        {
            int len = data[pos++];
            if ((len & 0x80) == 0) return len;
            int bytesCount = len & 0x7F;
            int val = 0;
            for (int i = 0; i < bytesCount; i++)
            {
                val = (val << 8) + data[pos++];
            }
            return val;
        }

        private byte[] TrimLeadingZero(byte[] input)
        {
            if (input.Length > 1 && input[0] == 0x00)
            {
                byte[] trimmed = new byte[input.Length - 1];
                Buffer.BlockCopy(input, 1, trimmed, 0, trimmed.Length);
                return trimmed;
            }
            return input;
        }
    }

    }
}
