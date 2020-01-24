using System.Reflection;
using UnityEditor.Performance.ProfileAnalyzer;
using UnityEngine;

[ExecuteInEditMode]
public class FrameItemSelected : MonoBehaviour
{
    public int FrameIndex;
    public string MarkerName;
    public int InstanceId;

    private ProfilerWindowInterface _profilerWindowInterface;

    private void Start()
    {
    }

    private void InitializePrifilerWindowInterface()
    {
        _profilerWindowInterface = new ProfilerWindowInterface();
        _profilerWindowInterface.OpenProfilerOrUseExisting();
    }

    [EditorButton]
    private void Update()
    {
        if(_profilerWindowInterface == null || _profilerWindowInterface.IsReady()) InitializePrifilerWindowInterface();
        var timeLineGUI = _profilerWindowInterface.GetTimeLineGUI();

        var m_SelectedEntryFieldInfo = (FieldInfo)typeof(ProfilerWindowInterface)
            .GetField("m_SelectedEntryFieldInfo", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(_profilerWindowInterface);
        var selectedEntry = m_SelectedEntryFieldInfo.GetValue(timeLineGUI);

        var m_SelectedEntryInstanceIdFieldInfo = (FieldInfo)typeof(ProfilerWindowInterface)
            .GetField("m_SelectedInstanceIdFieldInfo", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(_profilerWindowInterface);
        InstanceId = (int)m_SelectedEntryInstanceIdFieldInfo.GetValue(selectedEntry);

        var m_SelectedFrameIdFieldInfo = (FieldInfo)typeof(ProfilerWindowInterface)
            .GetField("m_SelectedFrameIdFieldInfo", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(_profilerWindowInterface);
        FrameIndex = (int)m_SelectedFrameIdFieldInfo.GetValue(selectedEntry);

        var m_SelectedNameFieldInfo = (FieldInfo)typeof(ProfilerWindowInterface)
            .GetField("m_SelectedNameFieldInfo", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(_profilerWindowInterface);
        MarkerName = (string)m_SelectedNameFieldInfo.GetValue(selectedEntry);
    }

    //private void FindDataForSelection()
    //{
    //    using (var frameData = ProfilerDriver.GetHierarchyFrameDataView(lastFrameIndex, threadIndex,
    //        HierarchyFrameDataView.ViewModes.Default, HierarchyFrameDataView.columnSelfPercent, false))
    //    {
    //        var rootId = frameData.GetRootItemID();
    //        if (rootId != -1)
    //        {
    //            var matchingItemId = GetItemIdMatchingRecursive(frameData, rootId,
    //                (name) => name.Contains(frameContains));

    //            if (matchingItemId != -1)
    //            {
    //                executeOnceFound(frameData, matchingItemId, frameIndex);
    //                return matchingItemId;
    //            }
    //        }
    //    }
    //}
}