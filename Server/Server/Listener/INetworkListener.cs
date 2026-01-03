namespace Server
{
    public interface INetworkListener
    {
        public void Start();

        public void Start(int backlog);

        public void Stop();
    }
}