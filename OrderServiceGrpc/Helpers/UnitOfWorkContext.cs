namespace OrderServiceGrpc.Helpers
{
    public class UnitOfWorkContext
    {
        public bool IsUnderUnitOfWork { get; set; }
        public void Begin() => IsUnderUnitOfWork = true;
        public void End() => IsUnderUnitOfWork = false;
    }
}
