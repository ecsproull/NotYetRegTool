using System;
using System.Windows.Threading;

namespace NotYetRegExTool
{
    /// <summary>
    /// Class used to send updates to the UI thread via a working thread.
    /// </summary>
    /// <typeparam name="T">Type of the control that this will send the update to.</typeparam>
    /// <typeparam name="S">Type of the parameter that is sent to the update method.</typeparam>
    public sealed class DelegateUpdateHelper<T, S>
    {
        private T m_controlClassInst;
        private Dispatcher m_dispatcher;
        private UpdateDelegate m_updateMethod;
        public delegate void UpdateDelegate(S param);

        public DelegateUpdateHelper(T controlInst, Dispatcher dispatcher, UpdateDelegate method)
        {
            m_controlClassInst = controlInst;
            m_updateMethod = method;
            m_dispatcher = dispatcher;
        }

        public DispatcherOperation UpdateAsync(S param)
        {
            return m_dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                                (Action)(() => m_updateMethod(param)));
        }
    }
}
