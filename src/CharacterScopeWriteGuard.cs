namespace ErenshorDeepSims
{
    // Pure commit predicate for delayed/background social persistence. A character switch advances
    // both the character-scope generation and conversation generation; either mismatch invalidates
    // the old thread before it can be written into the current character's memory store.
    internal static class CharacterScopeWriteGuard
    {
        internal static bool CanCommit(int capturedCharacterGeneration, int currentCharacterGeneration,
            int capturedConversationGeneration, int currentConversationGeneration)
        {
            return capturedCharacterGeneration == currentCharacterGeneration &&
                   capturedConversationGeneration == currentConversationGeneration;
        }
    }
}
