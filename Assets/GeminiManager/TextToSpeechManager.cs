using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleTextToSpeech.Scripts.Data;
using TMPro;
using System;
using ReadyPlayerAvatar = ReadyPlayerMe.Core;

namespace GoogleTextToSpeech.Scripts
{
    public class TextToSpeechManager : MonoBehaviour
    {
        [SerializeField] private VoiceScriptableObject voice;
        [SerializeField] private TextToSpeech text_to_speech;
        // [SerializeField] private AudioSource audioSource;

        private Action<AudioClip> _audioClipReceived;
        private Action<BadRequestData> _errorReceived;
        public ReadyPlayerAvatar.VoiceHandler voiceHandler; 

        // dedicated audio source for TTS playback to avoid playing back microphone recordings
        private AudioSource ttsAudioSource;
        private AudioSource previousVoiceHandlerSource;

        [Header("ElevenLabs TTS")]
        public bool useElevenLabs = false;
        public string elevenLabsApiKey = ""; // set in Inspector (do NOT commit keys)
        public string elevenLabsVoiceId = "alloy";

        [Header("Debug")]
        public bool forceVoiceHandlerAudioClipMode = true;
        public bool playTestOnStart = false;
        [TextArea(1,3)] public string testText = "Hello. This is a test response from text to speech.";

        private void Awake()
        {
            Debug.Log("TextToSpeechManager: Awake on " + gameObject.name);
        }

        private void OnEnable()
        {
            Debug.Log("TextToSpeechManager: OnEnable on " + gameObject.name);
        }

        private void Start()
        {
            Debug.Log("TextToSpeechManager: Start on " + gameObject.name);
            if (forceVoiceHandlerAudioClipMode && voiceHandler != null)
            {
                voiceHandler.AudioProvider = ReadyPlayerAvatar.AudioProviderType.AudioClip;
                voiceHandler.InitializeAudio();
                Debug.Log("TextToSpeechManager: Forced VoiceHandler to AudioClip mode.");
            }

            if (playTestOnStart)
            {
                SendTextToGoogle(testText);
            }
        }

        
        public void SendTextToGoogle(string _text)
        {
            // assign callbacks (avoid += to prevent duplicates)
            _errorReceived = ErrorReceived;
            _audioClipReceived = AudioClipReceived;

            Debug.Log($"TextToSpeechManager: Requesting TTS via {(useElevenLabs?"ElevenLabs":"Google TTS")} for text: {_text}");

            if (useElevenLabs)
            {
                ElevenLabsTTS.GetSpeechAudioFromElevenLabs(this, _text, elevenLabsApiKey, elevenLabsVoiceId, _audioClipReceived, _errorReceived);
            }
            else
            {
                text_to_speech.GetSpeechAudioFromGoogle(_text, voice, _audioClipReceived, _errorReceived);
            }
        }

        private void ErrorReceived(BadRequestData badRequestData)
        {
            Debug.Log($"Error {badRequestData.error.code} : {badRequestData.error.message}");
        }

        private void AudioClipReceived(AudioClip clip)
        {
            if (voiceHandler == null)
            {
                Debug.LogWarning("TextToSpeechManager: voiceHandler not assigned; cannot play TTS.");
                return;
            }

            // Ensure we have a dedicated TTS audio source on the same GameObject as the voice handler
            if (ttsAudioSource == null)
            {
                var go = new GameObject("TTS_AudioSource");
                go.transform.SetParent(voiceHandler.transform, false);
                ttsAudioSource = go.AddComponent<AudioSource>();
                ttsAudioSource.playOnAwake = false;
                ttsAudioSource.loop = false;
            }

            // Mark that TTS is playing to suppress STT
            ConversationState.IsPlayingTTS = true;

            // Swap the voiceHandler.AudioSource to the dedicated TTS source so lip-sync reads the playback
            previousVoiceHandlerSource = voiceHandler.AudioSource;
            voiceHandler.AudioSource = ttsAudioSource;

            ttsAudioSource.Stop();
            ttsAudioSource.clip = clip;
            ttsAudioSource.Play();

            // Restore after playback finishes
            StartCoroutine(RestoreAfterPlayback(clip.length));
        }

        private System.Collections.IEnumerator RestoreAfterPlayback(float seconds)
        {
            yield return new WaitForSeconds(seconds + 0.1f);

            // restore original audio source reference for the voice handler
            if (voiceHandler != null && previousVoiceHandlerSource != null)
            {
                voiceHandler.AudioSource = previousVoiceHandlerSource;
            }

            ConversationState.IsPlayingTTS = false;
        }
    }
}

