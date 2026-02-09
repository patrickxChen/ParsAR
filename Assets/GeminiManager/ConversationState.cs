using System;

public static class ConversationState
{
    // When true, Speech-to-Text should ignore microphone input to avoid feedback loops
    public static bool IsPlayingTTS = false;
}
