public interface IImplicitTransitionable
{
    /// <summary>
    /// True khi state đã hoàn thành và cần tự động chuyển về Locomotion.
    /// </summary>
    bool IsFinished { get; }
}
