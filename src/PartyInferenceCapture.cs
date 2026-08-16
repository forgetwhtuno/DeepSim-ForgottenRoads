namespace ErenshorDeepSims
{
    internal sealed class PartyInferenceCapture
    {
        internal readonly WorldSnapshot World;
        internal readonly SimSnapshot Speaker;
        internal readonly PartyGroundingRequestContext Request;

        internal PartyInferenceCapture(WorldSnapshot world, SimSnapshot speaker, PartyGroundingRequestContext request)
        {
            World = world;
            Speaker = speaker;
            Request = request;
        }
    }
}
