namespace Raisin.EventSystem
{
    public interface IEventSubscriber<T> where T : EventSystemEventArgs
    {
        public void ExecuteEvent(object sender, T eventArgs);
        public void DestroySubscriber();
    }
}
