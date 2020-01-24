namespace UnityEventCallRedirector.Attribute
{
    using System;

    /// <summary>
    /// Indicates that the property's backing field is serialized.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class UnityEventCallRedirectorAttribute : Attribute
    {
        public string EventInterceptorTypeName { get; }
        public string ReplaceCallsFromNamespacesRegex { get; }
        public string ExcludeFullMethodNameRegex { get; set; } = "";

        public UnityEventCallRedirectorAttribute(string eventInterceptorTypeName, string replaceCallsFromNamespacesRegex)
        {
            EventInterceptorTypeName = eventInterceptorTypeName;
            ReplaceCallsFromNamespacesRegex = replaceCallsFromNamespacesRegex;
        }
    }
}
