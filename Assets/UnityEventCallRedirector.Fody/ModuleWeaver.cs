using Mono.Cecil.Rocks;
using UnityEventCallRedirector.Attribute;

namespace UnityEventCallRedirector.Fody
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using global::Fody;
    using Mono.Cecil;
    using Mono.Cecil.Cil;
    using System.Reflection;
    using UnityEngine.Profiling;

    public sealed class ModuleWeaver : BaseModuleWeaver
    {
        private static readonly string FullAttributeName = typeof(UnityEventCallRedirectorAttribute).FullName;

        private const string FallbackSampleNameFormatName = "FallbackSampleNameFormat";
        private const string FallbackReplaceCallsFromNamespacesRegexName = "FallbackReplaceCallsFromNamespacesRegex";
        private const string FallbackExcludeFullMethodNameRegexName = "FallbackExcludeFullMethodNameRegex";
        private const string DefaultFallbackSampleNameFormat = "____{0} ({1}) <{2}>____";
        private const string DefaultFallbackReplaceCallsFromNamespacesRegex = ".*";
        private const string DefaultFallbackExcludeFullMethodNameRegex = "";

        private const string UnityEventTypeName = "UnityEvent`1";

        private MethodDefinition _interceptMethod;
        private MethodDefinition _unityEventInvokeMethod;
        private TypeDefinition _eventInterceptorType;
        private string _replaceCallsFromNamespacesRegex;
        private string _excludeFullMethodNameRegex;
        private MethodReference _unityObjectGetName;
        private MethodReference _objectGetType;
        private MethodReference _memberInfoGetName;
        private MethodReference _stringFormat;
        private MethodReference _beginSample;
        private MethodReference _endSample;
        private CustomAttribute _unityEventCallRedirectorAttribute;
        private string _fallbackSampleFormat;

        public override bool ShouldCleanReference => true;

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "UnityEngine";
        }


        public override void Execute()
        {
            if (!TryFindRequiredAssemblyAttribute())
            {
                LogInfo($"Assembly :{ModuleDefinition.Assembly.Name} does not specify attribute " +
                        $"{FullAttributeName} which is needed for processing. Falling back to inline IL rewrite");
                LoadXmlSetup();
            }
            else
            {
                LoadAttributeSetup();
            }

            FindReferences();

            var methodWithInstructionsToReplace = FindMethodsWithInstructionsCallingUnityEvent(_unityEventInvokeMethod);
            InterceptUnityEventInvokeCalls(methodWithInstructionsToReplace);
        }

        private void InterceptUnityEventInvokeCalls(List<MethodDefinitionInstructionToReplacePair> methodWithInstructionsToReplace)
        {
            foreach (var methodWithInstructionToReplace in methodWithInstructionsToReplace)
            {
                var method = methodWithInstructionToReplace.MethodDefinition;
                var instruction = methodWithInstructionToReplace.Instruction;
                var il = method.Body.GetILProcessor();

                method.Body.SimplifyMacros();
                if (_interceptMethod != null)
                {
                    var callValueGenericParameter = ((GenericInstanceType)((MethodReference)instruction.Operand).DeclaringType).GenericArguments[0];
                    var genericInterceptMethod = new GenericInstanceMethod(_interceptMethod);
                    genericInterceptMethod.GenericArguments.Add(callValueGenericParameter);

                    if (method.IsStatic)
                        il.InsertBefore(instruction, il.Create(OpCodes.Ldstr, "Static:" + method.FullName));
                    else
                        il.InsertBefore(instruction, il.Create(OpCodes.Ldarg_0));
                    il.InsertBefore(instruction, il.Create(OpCodes.Ldstr, method.Name));
                    il.Replace(instruction, il.Create(OpCodes.Call, genericInterceptMethod));

                    LogDebug($"Redirected: {method.DeclaringType.Name}::{method.Name} via interceptor");
                }
                else
                {
                    il.InsertBefore(instruction, il.Create(OpCodes.Ldstr, _fallbackSampleFormat));
                    if (method.IsStatic)
                    {
                        il.InsertBefore(instruction, il.Create(OpCodes.Ldstr, "Static" + method.FullName));
                        il.InsertBefore(instruction, il.Create(OpCodes.Ldstr, "Static" + method.FullName));
                    }
                    else
                    {
                        il.InsertBefore(instruction, il.Create(OpCodes.Ldarg_0));
                        il.InsertBefore(instruction, il.Create(OpCodes.Callvirt, _unityObjectGetName));
                        il.InsertBefore(instruction, il.Create(OpCodes.Ldarg_0));
                        il.InsertBefore(instruction, il.Create(OpCodes.Callvirt, _objectGetType));
                        il.InsertBefore(instruction, il.Create(OpCodes.Callvirt, _memberInfoGetName));
                    }


                    il.InsertBefore(instruction, il.Create(OpCodes.Ldstr, method.Name));
                    il.InsertBefore(instruction, il.Create(OpCodes.Call, _stringFormat));
                    if (method.IsStatic)
                        il.InsertBefore(instruction, il.Create(OpCodes.Ldnull));
                    else
                        il.InsertBefore(instruction, il.Create(OpCodes.Ldarg_0));
                    
                    il.InsertBefore(instruction, il.Create(OpCodes.Call, _beginSample));
                    
                    il.InsertAfter(instruction, il.Create(OpCodes.Call, _endSample));

                    LogDebug($"{ModuleDefinition.Assembly.Name} Redirected: {method.DeclaringType.Name}::{method.Name} via fallback inline IL");
                }
                method.Body.OptimizeMacros();
            }

            LogInfo($"{ModuleDefinition.Assembly.Name} Redirected: {methodWithInstructionsToReplace.Count} calls via {(_interceptMethod != null ? "interceptor" : "fallback inline IL")}");
        }

        private List<MethodDefinitionInstructionToReplacePair> FindMethodsWithInstructionsCallingUnityEvent(MethodDefinition unityEventInvokeMethod)
        {
            var methodWithInstructionsToReplace = new List<MethodDefinitionInstructionToReplacePair>();

            foreach (var t in ModuleDefinition.Types
                .Where(t => Regex.IsMatch(t.Namespace, _replaceCallsFromNamespacesRegex))
                .Where(t => t != _eventInterceptorType))
            {
                foreach (var method in t.Methods)
                {
                    if (!string.IsNullOrEmpty(_excludeFullMethodNameRegex)
                        && Regex.IsMatch($"{method.DeclaringType.FullName}::{method.FullName}", _excludeFullMethodNameRegex))
                    {
                        Console.WriteLine(
                            $"Skipping rewrite for excluded method: '{method.DeclaringType.FullName}::{method.FullName}'");
                        continue;
                    }

                    if (method.Body != null)
                    {
                        foreach (var instruction in method.Body.Instructions)
                        {
                            if ((instruction.Operand as MethodReference)?.Resolve() == unityEventInvokeMethod)
                            {
                                methodWithInstructionsToReplace.Add(
                                    new MethodDefinitionInstructionToReplacePair(method, instruction));
                            }
                        }
                    }
                }
            }

            return methodWithInstructionsToReplace;
        }

        private void FindReferences()
        {
            var unityEngineAssemblyFullReference = ModuleDefinition.AssemblyReferences.First(ar => ar.Name == "UnityEngine.CoreModule");
            var unityAssembly = ModuleDefinition.AssemblyResolver.Resolve(unityEngineAssemblyFullReference);

            var unityEventType = ModuleDefinition.ImportReference(unityAssembly.MainModule.Types.First(t => t.Name == UnityEventTypeName)).Resolve();
            _unityEventInvokeMethod = ModuleDefinition.ImportReference(unityEventType.Methods.First(m => m.Name == "Invoke" && m.Parameters.Count == 1)).Resolve();

            _unityObjectGetName = ImportPropertyGetter(typeof(UnityEngine.Object), p => p.Name == nameof(UnityEngine.Object.name));
            _objectGetType = ImportMethod(typeof(object), m => m.Name == nameof(object.GetType));
            _memberInfoGetName = ImportPropertyGetter(typeof(MemberInfo), m => m.Name == nameof(MemberInfo.Name));
            _stringFormat = ImportMethod(typeof(string),
                m => m.FullName == "System.String System.String::Format(System.String,System.Object,System.Object,System.Object)");

            _beginSample = ImportMethod(typeof(Profiler), m => m.Name == nameof(Profiler.BeginSample) && m.Parameters.Count == 2);
            _endSample = ImportMethod(typeof(Profiler), m => m.Name == nameof(Profiler.EndSample));
        }

        private bool TryFindRequiredAssemblyAttribute()
        {
            _unityEventCallRedirectorAttribute = ModuleDefinition.Assembly.CustomAttributes
                .FirstOrDefault(a => a.AttributeType.FullName == FullAttributeName);

            return _unityEventCallRedirectorAttribute != null;
        }

        private void LoadAttributeSetup()
        {
            var eventInterceptorTypeName = _unityEventCallRedirectorAttribute.ConstructorArguments[0].Value.ToString();

            _eventInterceptorType = ModuleDefinition.Types.First(t => t.Name == eventInterceptorTypeName);
            _interceptMethod = _eventInterceptorType.Methods
                .First(m => m.Name == "Intercept" && m.GenericParameters.Count == 1);

            _replaceCallsFromNamespacesRegex = _unityEventCallRedirectorAttribute.ConstructorArguments[1].Value?.ToString();
            _excludeFullMethodNameRegex = _unityEventCallRedirectorAttribute.Properties.Single(p => p.Name == nameof(UnityEventCallRedirectorAttribute.ExcludeFullMethodNameRegex)).Argument.Value?.ToString();
        }

        private void LoadXmlSetup()
        {
            _fallbackSampleFormat = Config.Attribute(FallbackSampleNameFormatName)?.Value ?? DefaultFallbackSampleNameFormat;
            _replaceCallsFromNamespacesRegex = Config.Attribute(FallbackReplaceCallsFromNamespacesRegexName)?.Value ?? DefaultFallbackReplaceCallsFromNamespacesRegex;
            _excludeFullMethodNameRegex = Config.Attribute(FallbackExcludeFullMethodNameRegexName)?.Value ?? DefaultFallbackExcludeFullMethodNameRegex;
        }

        private MethodReference ImportMethod(Type type, Func<MethodDefinition, bool> methodPredicate)
        {
            return ModuleDefinition.ImportReference(ModuleDefinition.ImportReference(type).Resolve().Methods.First(methodPredicate));
        }

        private MethodReference ImportPropertyGetter(Type type, Func<PropertyDefinition, bool> propertyPredicate)
        {
            var prop = ModuleDefinition.ImportReference(type).Resolve().Properties.First(propertyPredicate);
            return ModuleDefinition.ImportReference(prop.GetMethod);
        }

        private class MethodDefinitionInstructionToReplacePair
        {
            public MethodDefinition MethodDefinition { get; }
            public Instruction Instruction { get; }

            public MethodDefinitionInstructionToReplacePair(MethodDefinition methodDefinition, Instruction instruction)
            {
                MethodDefinition = methodDefinition;
                Instruction = instruction;
            }
        }
    }
}
