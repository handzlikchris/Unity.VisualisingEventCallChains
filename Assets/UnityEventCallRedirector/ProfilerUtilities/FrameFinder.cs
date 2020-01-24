using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Performance.ProfileAnalyzer;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

public class FrameFinder : MonoBehaviour
{
    public string FrameContains = "";
    public int StartFrame = 0;
    public int EndFrame = -1;
    public int ThreadIndex = 0;
    public string LookInThread = "1:Main Thread";

    private int LastFoundAtFrameIndex = 0;
    
    [EditorButton]
    private void ShowInProfiler()
    {
        ExecuteOnFoundProfilerFrame(FrameContains, ThreadIndex, StartFrame, 
            EndFrame != -1 ? EndFrame : ProfilerDriver.lastFrameIndex,
            ShowFoundItemInProfiler);
    }

    private void ShowFoundItemInProfiler(HierarchyFrameDataView frameData, int matchingItemId, int frameIndex)
    {
        var goInstanceId = frameData.GetItemInstanceID(matchingItemId);
        var foundObject = EditorUtility.InstanceIDToObject(goInstanceId);
        //Selection.activeObject = foundObject;
        EditorGUIUtility.PingObject(foundObject);

        var profilerWindowInterface = new ProfilerWindowInterface();
        profilerWindowInterface.OpenProfilerOrUseExisting();
        profilerWindowInterface.JumpToFrame(frameIndex + 1);

        var markerName = frameData.GetItemName(matchingItemId);
        profilerWindowInterface.SetProfilerWindowMarkerName(markerName, new List<string>() {LookInThread});

        ProfilerDriver.enabled = false;
        EditorApplication.isPaused = true;
    }

    [EditorButton]
    private void FindNext()
    {
        ExecuteOnFoundProfilerFrame(FrameContains, ThreadIndex, LastFoundAtFrameIndex + 1,
            EndFrame != -1 ? EndFrame : ProfilerDriver.lastFrameIndex,
            ShowFoundItemInProfiler);
    }

    public int ExecuteOnFoundProfilerFrame(string frameContains, int threadIndex, int startFrame, int lastFrameIndex, 
        Action<HierarchyFrameDataView, int, int> executeOnceFound)
    {
        for (var frameIndex = startFrame; frameIndex <= lastFrameIndex; frameIndex++)
        {
            using (var frameData = ProfilerDriver.GetHierarchyFrameDataView(frameIndex, threadIndex,
                HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnSelfPercent, false))
            {
                var rootId = frameData.GetRootItemID();
                if (rootId != -1)
                {
                    var matchingItemId = GetItemIdMatchingRecursive(frameData, rootId,
                        (name) => name.Contains(frameContains));

                    if (matchingItemId != -1)
                    {
                        LastFoundAtFrameIndex = frameIndex;
                        executeOnceFound(frameData, matchingItemId, frameIndex);
                        return matchingItemId;
                    }
                }
            }
        }

        return -1;
    }

    private static int GetItemIdMatchingRecursive(HierarchyFrameDataView frameData, int itemId, Func<string, bool> predicate)
    {
        string searchString = "";

        var functionName = frameData.GetItemName(itemId);
        searchString += functionName;
        var goInstanceId = frameData.GetItemInstanceID(itemId);
        if (goInstanceId > 0)
        {
            var go = EditorUtility.InstanceIDToObject(goInstanceId);
            var typeName = go.GetType().Name;
            searchString += $" - {go.name} ({typeName})";
        }

        var isMatch = predicate(searchString);
        if (isMatch)
            return itemId;
        else
        {
            var itemChildrenIds = new List<int>();
            frameData.GetItemChildren(itemId, itemChildrenIds);
            foreach (var childId in itemChildrenIds)
            {
                var foundId = GetItemIdMatchingRecursive(frameData, childId, predicate);
                if (foundId != -1) return foundId;
            }
        }

        return -1;
    }
}
