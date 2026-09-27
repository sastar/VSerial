using System.Runtime.InteropServices;
using System.Text;

namespace VSerial.Interop;

public static partial class NativeMethods
{
    private const string CFGMGR32_LIB = "cfgmgr32.dll";

    /// <summary>
    /// 获取指定设备实例的设备实例 ID 字符串 (CM_Get_Device_IDW)
    /// </summary>
    [DllImport(CFGMGR32_LIB, CharSet = CharSet.Unicode)]
    public static extern uint CM_Get_Device_IDW(
        uint dnDevInst,
        StringBuilder Buffer,
        uint BufferLen,
        uint ulFlags
    );

    /// <summary>
    /// 获取指定设备节点的运行状态标志与问题错误码 (CM_Get_DevNode_Status)
    /// </summary>
    [DllImport(CFGMGR32_LIB)]
    public static extern uint CM_Get_DevNode_Status(
        out uint pulStatus,
        out uint pulProblemNumber,
        uint dnDevInst,
        uint ulFlags
    );

    /// <summary>
    /// 重新枚举指定设备节点及其子树 (CM_Reenumerate_DevNode)
    /// </summary>
    [DllImport(CFGMGR32_LIB)]
    public static extern uint CM_Reenumerate_DevNode(
        uint dnDevInst,
        uint ulFlags
    );
}
