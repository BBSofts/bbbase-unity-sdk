using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace BBBaseSdk
{
    /// <summary>
    /// UnityWebRequest 를 async/await 로 쓸 수 있게 해주는 awaiter.
    /// 사용: <c>await unityWebRequest.SendWebRequest();</c>
    /// </summary>
    public struct UnityWebRequestAwaiter : INotifyCompletion
    {
        private readonly UnityWebRequestAsyncOperation _op;

        public UnityWebRequestAwaiter(UnityWebRequestAsyncOperation op) => _op = op;

        public bool IsCompleted => _op.isDone;

        public void GetResult() { }

        public void OnCompleted(System.Action continuation)
        {
            if (_op.isDone) continuation();
            else _op.completed += _ => continuation();
        }
    }

    public static class UnityWebRequestAwaiterExtensions
    {
        public static UnityWebRequestAwaiter GetAwaiter(this UnityWebRequestAsyncOperation op) =>
            new UnityWebRequestAwaiter(op);
    }
}
