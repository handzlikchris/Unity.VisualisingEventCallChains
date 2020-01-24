using Assets.UnityEventCallRedirector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Profiling;
using UnityEventCallRedirector.Attribute;

[assembly: UnityEventCallRedirector(
    eventInterceptorTypeName: nameof(EventInterceptor),
    replaceCallsFromNamespacesRegex: ".*",
    ExcludeFullMethodNameRegex = "ObservableList.+::Start"
)]
 
namespace Assets.UnityEventCallRedirector
{
    public class EventInterceptor
    {
        public static void Intercept<TArg>(UnityEvent<TArg> originalEvent, TArg arg, object self, string callingMethodName)
        {
            Profiler.BeginSample($"____{((MonoBehaviour)self).name} ({self.GetType().Name}) <{callingMethodName}>____+", (MonoBehaviour)self);
            originalEvent.Invoke(arg);
            Profiler.EndSample();
        }
    }
}
