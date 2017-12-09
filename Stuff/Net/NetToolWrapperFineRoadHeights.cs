namespace NetworkSkins.Net
{
    public class NetToolWrapperFineRoadHeights : INetToolWrapper
    {
        private readonly NetToolWrapperVanilla vanilla = new NetToolWrapperVanilla();

        public NetInfo GetCurrentPrefab()
        {
            var netTool = ToolsModifierControl.GetCurrentTool<NetTool>();

            return netTool != null ? netTool.m_prefab : vanilla.GetCurrentPrefab();
        }
    }
}
