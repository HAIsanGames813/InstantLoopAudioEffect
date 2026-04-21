using YukkuriMovieMaker.Plugin;

namespace InstantLoopPlugin
{
    /// <summary>
    /// プラグインエントリポイント
    /// </summary>
    public class InstantLoopPlugin : IPlugin
    {
        public string Name => "瞬間ループ";
        public string Version => "1.0.0";
        public string Description => "音声アイテムの指定区間を繰り返すぶつ切りループエフェクト。";
    }
}