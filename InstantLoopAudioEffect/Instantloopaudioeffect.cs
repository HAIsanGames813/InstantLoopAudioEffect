using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;

namespace InstantLoopPlugin
{
    /// <summary>
    /// 瞬間ループ音声エフェクト
    ///
    /// 指定した「開始位置」から「再生間隔」ぶんだけ音声を再生し、
    /// それを「回数」で指定した回数だけ繰り返す（ぶつ切りループ）。
    /// 回数 = 0 の場合は音声アイテム終端まで無限ループ。
    /// </summary>
    [AudioEffect(
        "瞬間ループ",
        ["音声エフェクト"],
        ["瞬間ループ", "ループ", "即リセット", "instant loop", "loop"])]
    public class InstantLoopAudioEffect : AudioEffectBase
    {
        /// <summary>
        /// 開始位置（ms）
        /// ループの折り返し先となるオフセット。音声先端が無音の場合に使用。
        /// </summary>
        [Display(GroupName = "瞬間ループ", Name = "開始位置",
            Description = "ループの折り返し先となる再生開始オフセット（ms）。音声先端が無音の場合はここでスキップできます。")]
        [AnimationSlider("F0", "ms", 0, 10000)]
        public Animation StartOffset { get; } = new Animation(0, 0, 100000);

        /// <summary>
        /// 再生間隔（ms）
        /// この時間ぶんだけ再生したら開始位置に戻る。
        /// </summary>
        [Display(GroupName = "瞬間ループ", Name = "再生間隔",
            Description = "ぶつ切りして開始位置から再生し直すまでの間隔（ms）。")]
        [AnimationSlider("F0", "ms", 1, 10000)]
        public Animation Interval { get; } = new Animation(500, 1, 100000);

        /// <summary>
        /// 繰り返し回数
        /// 0 = 無限、n = n回ぶつ切り後に通常再生。
        /// </summary>
        [Display(GroupName = "瞬間ループ", Name = "回数",
            Description = "ぶつ切りを繰り返す回数。0で音声終端まで無限ループ。")]
        [AnimationSlider("F0", "回", 0, 100)]
        public Animation RepeatCount { get; } = new Animation(0, 0, 10000);

        public override string Label => "瞬間ループ";

        // AviUtlのexoフィルタ文字列は不要なので空を返す
        public override IEnumerable<string> CreateExoAudioFilters(int sampleRate, ExoOutputDescription description)
            => [];

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
            => new InstantLoopAudioEffectProcessor(this);

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => [StartOffset, Interval, RepeatCount];
    }
}