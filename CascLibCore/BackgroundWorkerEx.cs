using System;
using System.ComponentModel;
using System.Threading;

namespace CASCLib
{
    //[DefaultEvent("DoWork"), HostProtection(SecurityAction.LinkDemand, SharedState = true)]
    //public class BackgroundWorkerEx : Component
    //{
    //    private AsyncOperation asyncOperation;
    //    private bool cancellationPending;
    //    private static readonly object doWorkKey = new object();
    //    private bool isRunning;
    //    private readonly SendOrPostCallback operationCompleted;
    //    private static readonly object progressChangedKey = new object();
    //    private readonly SendOrPostCallback progressReporter;
    //    private static readonly object runWorkerCompletedKey = new object();
    //    private readonly WorkerThreadStartDelegate threadStart;

    //    public event DoWorkEventHandler DoWork
    //    {
    //        add
    //        {
    //            base.Events.AddHandler(doWorkKey, value);
    //        }
    //        remove
    //        {
    //            base.Events.RemoveHandler(doWorkKey, value);
    //        }
    //    }

    //    public event ProgressChangedEventHandler ProgressChanged
    //    {
    //        add
    //        {
    //            base.Events.AddHandler(progressChangedKey, value);
    //        }
    //        remove
    //        {
    //            base.Events.RemoveHandler(progressChangedKey, value);
    //        }
    //    }

    //    public event RunWorkerCompletedEventHandler RunWorkerCompleted
    //    {
    //        add
    //        {
    //            base.Events.AddHandler(runWorkerCompletedKey, value);
    //        }
    //        remove
    //        {
    //            base.Events.RemoveHandler(runWorkerCompletedKey, value);
    //        }
    //    }

    //    public BackgroundWorkerEx()
    //    {
    //        this.threadStart = new WorkerThreadStartDelegate(this.WorkerThreadStart);
    //        this.operationCompleted = new SendOrPostCallback(this.AsyncOperationCompleted);
    //        this.progressReporter = new SendOrPostCallback(this.ProgressReporter);
    //    }

    //    private void AsyncOperationCompleted(object arg)
    //    {
    //        this.isRunning = false;
    //        this.cancellationPending = false;
    //        this.OnRunWorkerCompleted((RunWorkerCompletedEventArgs)arg);
    //        this.asyncOperation = null;
    //    }

    //    public void CancelAsync()
    //    {
    //        this.cancellationPending = true;
    //    }

    //    protected virtual void OnDoWork(DoWorkEventArgs e)
    //    {
    //        DoWorkEventHandler handler = (DoWorkEventHandler)base.Events[doWorkKey];
    //        if (handler != null)
    //        {
    //            handler(this, e);
    //        }
    //    }

    //    protected virtual void OnProgressChanged(ProgressChangedEventArgs e)
    //    {
    //        ProgressChangedEventHandler handler = (ProgressChangedEventHandler)base.Events[progressChangedKey];
    //        if (handler != null)
    //        {
    //            handler(this, e);
    //        }
    //    }

    //    protected virtual void OnRunWorkerCompleted(RunWorkerCompletedEventArgs e)
    //    {
    //        RunWorkerCompletedEventHandler handler = (RunWorkerCompletedEventHandler)base.Events[runWorkerCompletedKey];
    //        if (handler != null)
    //        {
    //            handler(this, e);
    //        }
    //    }

    //    private void ProgressReporter(object arg)
    //    {
    //        this.OnProgressChanged((ProgressChangedEventArgs)arg);
    //    }

    //    public void ReportProgress(int percentProgress)
    //    {
    //        this.ReportProgress(percentProgress, null);
    //    }

    //    public void ReportProgress(int percentProgress, object userState)
    //    {
    //        ProgressChangedEventArgs arg = new ProgressChangedEventArgs(percentProgress, userState);
    //        if (this.asyncOperation != null)
    //        {
    //            this.asyncOperation.Post(this.progressReporter, arg);
    //        }
    //        else
    //        {
    //            this.progressReporter(arg);
    //        }
    //    }

    //    public void RunWorkerAsync()
    //    {
    //        this.RunWorkerAsync(null);
    //    }

    //    public void RunWorkerAsync(object argument)
    //    {
    //        if (this.isRunning)
    //        {
    //            throw new InvalidOperationException("BackgroundWorker_WorkerAlreadyRunning");
    //        }
    //        this.isRunning = true;
    //        this.cancellationPending = false;
    //        this.asyncOperation = AsyncOperationManager.CreateOperation(null);
    //        this.threadStart.BeginInvoke(argument, null, null);
    //    }

    //    private void WorkerThreadStart(object argument)
    //    {
    //        object result = null;
    //        Exception error = null;
    //        bool cancelled = false;
    //        try
    //        {
    //            DoWorkEventArgs e = new DoWorkEventArgs(argument);
    //            this.OnDoWork(e);
    //            if (e.Cancel)
    //            {
    //                cancelled = true;
    //            }
    //            else
    //            {
    //                result = e.Result;
    //            }
    //        }
    //        catch (OperationCanceledException)
    //        {
    //            cancelled = true;
    //        }
    //        catch (Exception exception1)
    //        {
    //            error = exception1;
    //        }
    //        RunWorkerCompletedEventArgs arg = new RunWorkerCompletedEventArgs(result, error, cancelled);
    //        this.asyncOperation.PostOperationCompleted(this.operationCompleted, arg);
    //    }

    //    [Browsable(false)]
    //    public bool CancellationPending
    //    {
    //        get
    //        {
    //            return this.cancellationPending;
    //        }
    //    }

    //    [Browsable(false)]
    //    public bool IsBusy
    //    {
    //        get
    //        {
    //            return this.isRunning;
    //        }
    //    }

    //    private delegate void WorkerThreadStartDelegate(object argument);

    //    public void ThrowOnCancel()
    //    {
    //        if (this.cancellationPending)
    //            throw new OperationCanceledException();
    //    }
    //}

    public class BackgroundWorkerEx : BackgroundWorker
    {
        private int lastProgressPercentage;

        public BackgroundWorkerEx()
        {
            WorkerReportsProgress = true;
            WorkerSupportsCancellation = true;
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            try
            {
                base.OnDoWork(e);
            }
            catch (OperationCanceledException)
            {
                e.Cancel = true;
            }
        }

        public new void ReportProgress(int percentProgress)
        {
            if (CancellationPending)
                throw new OperationCanceledException();

            if (IsBusy && percentProgress > lastProgressPercentage)
                base.ReportProgress(percentProgress);

            lastProgressPercentage = percentProgress;
        }

        public new void ReportProgress(int percentProgress, object userState)
        {
            if (CancellationPending)
                throw new OperationCanceledException();

            if (IsBusy)
                base.ReportProgress(percentProgress, userState);

            lastProgressPercentage = percentProgress;
        }
    }
}
