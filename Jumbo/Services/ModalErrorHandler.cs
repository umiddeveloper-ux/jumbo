namespace Jumbo.Services
{
    /// <summary>
    /// Modal Error Handler.
    /// </summary>
    public class ModalErrorHandler : IErrorHandler
    {
        SemaphoreSlim _semaphore = new(1, 1);

        /// <summary>
        /// Handle error in UI.
        /// </summary>
        /// <param name="ex">Exception.</param>
        public void HandleError(Exception ex)
        {
#if DEBUG
            // Dialogs do not show up in screenshots: keep a trace of what went wrong.
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "jumbo-errors.log"), DateTime.Now + " " + ex + Environment.NewLine); } catch { }
#endif
            DisplayAlertAsync(ex).FireAndForgetSafeAsync();
        }

        // What to tell the learner (null = nothing: a rejected login is handled by UnauthorizedHandler).
        static string? Describe(Exception ex)
        {
            if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized }) return null;
            if (ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException || ex is IOException)
                return LocalizationManager.Get("Error_Network");
            // the text of a system exception can be only a key of a resource (net_http_message_...): that is not for the learner
            var m = ex.Message;
            return string.IsNullOrWhiteSpace(m) || (!m.Contains(' ') && m.Contains('_')) ? LocalizationManager.Get("Error_Generic") : m;
        }

        async Task DisplayAlertAsync(Exception ex)
        {
            try
            {
                await _semaphore.WaitAsync();
                var text = Describe(ex);
                if (text is not null && Shell.Current is Shell shell)
                    await shell.DisplayAlertAsync(LocalizationManager.Get("Error_Title"), text, "OK");
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}