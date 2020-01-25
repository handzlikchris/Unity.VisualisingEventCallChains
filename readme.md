



# Visualising Unity Event Call Chains

If you worked with `UnityEvents` you know that it's quite good way to decouple component logic and use events to pass data. You probably also found that when those call-chains get deep it's quite difficult to understand what's happening from wider perspective.

This tool will help you visualise that workflow using Unity profiler, instead of going through the chain from node to node you can see **big picture** with profiler window while still easily navigating between game object nodes
![Event Call Chain Profiler view](/_github/event-call-chain-profiler-view_small)


That should allow you to have much better understanding of what's actually happening.
![Event Call Chain Visualisation Workflow](/_github/ViewingUnityEventInvokeCallsInProfiler_workflow.gif)

Which helps a lot if you have a solution that already uses event call chains extensively
![Event Call Chain Visualisation Workflow](/_github/ViewingUnityEventInvokeCallsInProfiler_VRTK.gif)
*from [VRTK Farm example](https://github.com/ExtendRealityLtd/VRTK) which uses call chains extensively*


## Approach
The tool will use IL Weaving and will redirect all the calls to `UnityEvent` **(with one generic argument)**, eg `UnityEvent<int>` or `UnityEvent<YourDataStructure>` to `EventInterceptor` where you could add any actions needed. 

Method signature and default implementation:
```
    public class EventInterceptor
    {
        public static void Intercept<TArg>(UnityEvent<TArg> originalEvent, TArg arg, object self, string callingMethodName)
        {
            Profiler.BeginSample($"____{((MonoBehaviour)self).name} ({self.GetType().Name}) <{callingMethodName}>____+", (MonoBehaviour)self);
            originalEvent.Invoke(arg);
            Profiler.EndSample();
        }
    }
```

## Setup
You can clone this repository and run it in Unity as an example.

To import into your project:
1) In Unity add a package dependency to [Malimbe]([https://github.com/ExtendRealityLtd/Malimbe](https://github.com/ExtendRealityLtd/Malimbe)) which will hook up to Unity build process so the weaver code can work on your assemblies after Unity is done compiling them.

You can do that via `manifest.json` file located in `/Packages` folder. You'll have to add following entries (as per Malimbe page)
```
  
  "scopedRegistries": [
    {
      "name": "npmjs",
      "url": "https://registry.npmjs.org/",
      "scopes": [
        "io.extendreality"
      ]
    }
  ],
  "dependencies": {
    "io.extendreality.malimbe": "9.6.5",
    ...
  }
}
```

2)  Download and import [UnityEventCallRedirector.unitypackage](/_github/UnityEventCallRedirector.unitypackage)
3) Recompile
- If you see an error
`'A configuration lists 'UnityEventCallRedirector' but the assembly file wasn't found in the search paths'`
That means `UnityEventCallRedirector.Fody` is not compiled, you can go to `ModuleWeaver.cs` and make some non-relevant change (like adding a space) followed by saving to make sure DLL is actually compiled
4) Now changes to your scripts will trigger recompile which will in turn trigger IL Weaving to intercept your calls

### Finding Interesting Frames
You can use `FrameFinder` script located in `UnityEventCallRedirector/ProfilerUtilities` to find your custom markers. 
- drop the script onto a game object
- change `FrameContains` to marker that you're after, for example, if you're looking for calls made from script `TestCallChain` by `Child-1-2-1` object you can put `Child-1-2-1 (TestCallChain)`.
- hit `ShowInProfiler`
- optionally you can navigate further with `FindNext`

### Configuring Interception
In the package, you'll find `EventInterceptor.cs` with `Intercept` method. You can adjust that as needed. There's also an assembly attribute specified `UnityEventCallRedirector` where you can configure some more options.

- `eventInterceptorTypeName` - if different than `EventInterceptor`
- `replaceCallsFromNamespacesRegex` - it'll narrow down types to be looked at when searching for `UnityEvent``1.Invoke` calls
- `ExcludeFullMethodNameRegex` - full method name (including) type regex to be excluded. eg. `ObservableList.+::Start`

### Using asmdef / Fallback Interception
Separate assemblies will not have access to that `EventInterceptor` when that happens interception will still be performed but using IL as defined in `ModuleWeaver` - it'll simply add `BeginSample` and `EndSample` around event call (and build proper string).

This is helpful if you like to weave external packages that you don't control (and don't wish to embed) - if you have control over assembly you can copy `EventInterceptor.cs` class with assembly attribute there.

### Configuring fallback interception
You can control few parameters of fallback IL weaving, it's done via XML attributes in `FodyWeavers.xml`
- `FallbackSampleNameFormat` - will control how you see entries in `Profiler Window`, there are 3 tokens that can be used and **refer to object that invokes the event**:
    - `{0}` - unity object name
    - `{1}` - unity object type name
    - `{2}` - unity object method name where event is being called from
 - `FallbackReplaceCallsFromNamespacesRegex` - it'll narrow down types to be looked at when searching for `UnityEvent``1.Invoke` calls
 - `FallbackExcludeFullMethodNameRegex` - full method name (including) type regex to be excluded. eg. `ObservableList.+::Start`


### Configuring Malimbe
You can further configure Malimbe via `FodyWeavers.xml` file, you'll find the details in their repository.


## Known issues
- Right now tool works just for `UnityEvent<arg>` - it doesn't work with no argument one or with more than 1 argument
- You may see some errors that weaving assembly is not available when starting Unity, this is to do with compilation order and should not cause you issues, it'll be gone on next compilation. I've included weaver as source code with asmdef so it's easier to modify weaver. Ideally compiled DLL should just be included
- There are some DLLs included with the package and also included in Malimbe package, it's not easy to get them referenced without embedding Malimbe package. It shouldn't give you troubles but if something funny happens it'd be worth to look if that's not the cause. 