using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using GoogleTextToSpeech.Scripts.Data;

public class ElevenLabsTTS : MonoBehaviour
{
    private const string Mp3FileName = "audio.mp3";

    [Serializable]
    private class VoicesResponse
    {
        public Voice[] voices;
    }

    [Serializable]
    private class Voice
    {
        public string voice_id;
        public string name;
    }

    [Serializable]
    private class ElevenRequest
    {
        public string text;
    }

    public static void GetSpeechAudioFromElevenLabs(MonoBehaviour host, string text, string apiKey, string voiceId, Action<AudioClip> audioClipReceived, Action<BadRequestData> errorReceived)
    {
        if (host == null)
        {
            Debug.LogError("ElevenLabsTTS: host MonoBehaviour is required to start coroutine.");
            return;
        }

        host.StartCoroutine(PostTTS(host, text, apiKey, voiceId, audioClipReceived, errorReceived));
    }

    private static IEnumerator PostTTS(MonoBehaviour host, string text, string apiKey, string voiceId, Action<AudioClip> audioClipReceived, Action<BadRequestData> errorReceived)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            errorReceived?.Invoke(new BadRequestData { error = new Error { code = 401, message = "ElevenLabs API key not provided." } });
            yield break;
        }

        string resolvedVoiceId = voiceId;
        if (string.IsNullOrWhiteSpace(resolvedVoiceId) || resolvedVoiceId == "alloy")
        {
            yield return host.StartCoroutine(ResolveDefaultVoiceId(apiKey, (id) => resolvedVoiceId = id));
        }

        if (string.IsNullOrWhiteSpace(resolvedVoiceId))
        {
            errorReceived?.Invoke(new BadRequestData { error = new Error { code = 404, message = "ElevenLabs voiceId not set or not found." } });
            yield break;
        }

        string url = $"https://api.elevenlabs.io/v1/text-to-speech/{resolvedVoiceId}";

        var req = new ElevenRequest { text = text };
        string json = JsonUtility.ToJson(req);
        byte[] payload = Encoding.UTF8.GetBytes(json);

        using var www = new UnityWebRequest(url, "POST");
        www.uploadHandler = new UploadHandlerRaw(payload);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        // ElevenLabs accepts either xi-api-key or Authorization: Bearer. Use xi-api-key header.
        www.SetRequestHeader("xi-api-key", apiKey);
        www.SetRequestHeader("Accept", "audio/mpeg");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("ElevenLabs TTS error: " + www.error + " code=" + www.responseCode);
            Debug.LogError("ElevenLabs error body: " + www.downloadHandler.text);
            errorReceived?.Invoke(new BadRequestData { error = new Error { code = (int)www.responseCode, message = www.error } });
            yield break;
        }

        // write bytes to temp mp3 file
        try
        {
            string path = Application.temporaryCachePath + "/" + Mp3FileName;
            File.WriteAllBytes(path, www.downloadHandler.data);
            Debug.Log($"ElevenLabsTTS: wrote {www.downloadHandler.data.Length} bytes to {path}");

            // Use existing AudioConverter to load the mp3 as an AudioClip
            var convHolder = new GameObject("ElevenLabs_AudioConverter");
            var conv = convHolder.AddComponent<GoogleTextToSpeech.Scripts.AudioConverter>();
            // Ensure file is available then call loader
            conv.LoadClipFromMp3(audioClipReceived);
            // cleanup GameObject after a short delay
            host.StartCoroutine(DestroyAfterDelay(convHolder, 5f));
        }
        catch (Exception e)
        {
            Debug.LogError("ElevenLabsTTS: failed to save or load audio - " + e);
            errorReceived?.Invoke(new BadRequestData { error = new Error { code = 500, message = e.Message } });
        }
    }

    private static IEnumerator DestroyAfterDelay(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go != null) GameObject.Destroy(go);
    }

    private static IEnumerator ResolveDefaultVoiceId(string apiKey, Action<string> onResolved)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            onResolved?.Invoke(null);
            yield break;
        }

        using var www = new UnityWebRequest("https://api.elevenlabs.io/v1/voices", "GET");
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("xi-api-key", apiKey);

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("ElevenLabs voice list error: " + www.error + " code=" + www.responseCode);
            Debug.LogError("ElevenLabs voice list body: " + www.downloadHandler.text);
            onResolved?.Invoke(null);
            yield break;
        }

        var resp = JsonUtility.FromJson<VoicesResponse>(www.downloadHandler.text);
        if (resp != null && resp.voices != null && resp.voices.Length > 0)
        {
            Debug.Log("ElevenLabsTTS: Using default voice: " + resp.voices[0].name + " (" + resp.voices[0].voice_id + ")");
            onResolved?.Invoke(resp.voices[0].voice_id);
        }
        else
        {
            onResolved?.Invoke(null);
        }
    }
}
