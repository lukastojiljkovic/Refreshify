namespace Refreshify.Core.Platform;

/// <summary>Runs shell APIs that use COM internally on a thread with a known apartment, off the UI thread.</summary>
public static class StaThread
{
    public static Task<T> Run<T>(Func<T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(work());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
