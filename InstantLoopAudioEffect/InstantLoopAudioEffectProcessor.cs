using YukkuriMovieMaker.Player.Audio;
using YukkuriMovieMaker.Player.Audio.Effects;

namespace InstantLoopPlugin
{
    /// <summary>
    /// 瞬間ループのオーディオプロセッサ
    ///
    /// AudioEffectProcessorBase を継承することで
    /// ・Position（現在の出力float位置）の管理
    /// ・Input プロパティ
    /// は基底クラスが面倒を見る。
    ///
    /// 【設計方針】
    /// Duration/Hz は Input から取得するためキャッシュ不要。
    /// seek() → Input を正しい位置にシーク。
    /// read() → 境界ごとにチャンクに分けて Input.Seek → Input.Read を繰り返す。
    ///
    /// 「途中からの再生」も seek() が呼ばれるので、
    /// 出力フレーム位置 → ソースフレーム位置のマッピングが一致する限り正常動作。
    /// </summary>
    internal class InstantLoopAudioEffectProcessor : AudioEffectProcessorBase
    {
        // ステレオ固定（YMM4の音声は基本ステレオ 2ch）
        const int Channels = 2;

        readonly InstantLoopAudioEffect _item;

        // IAudioStream から継承する Duration / Hz は Input 経由で取得
        public override int Hz => Input?.Hz ?? 0;

        /// <summary>Duration は floatサンプル総数（フレーム数 × Channels）</summary>
        public override long Duration => Input?.Duration ?? 0;

        public InstantLoopAudioEffectProcessor(InstantLoopAudioEffect item)
        {
            _item = item;
        }

        // ────────────────────────────────────────────────────────────────────
        // seek：YMM4 から「ここから再生して」と指示される
        //        position は出力 float 位置
        // ────────────────────────────────────────────────────────────────────
        protected override void seek(long position)
        {
            if (Input is null) return;

            long sourcePos = MapOutputToSourcePos(position);
            Input.Seek(sourcePos);
        }

        // ────────────────────────────────────────────────────────────────────
        // read：バッファに書き込む（基底クラスが Position を更新してくれる）
        // ────────────────────────────────────────────────────────────────────
        protected override int read(float[] buffer, int offset, int count)
        {
            if (Input is null) return 0;

            int written = 0;

            while (written < count)
            {
                int remaining = count - written;

                // 現在の出力 float 位置（基底クラスの Position に書き込み済み分を加算）
                long currentOutputPos = Position + written;
                long currentFrame = currentOutputPos / Channels;
                long totalFrames = Duration / Channels;

                // ── パラメータ取得 ────────────────────────────────────────
                long startOffsetFrames = MsToFrames(_item.StartOffset.GetValue(currentFrame, totalFrames, Hz));
                long intervalFrames = Math.Max(1L, MsToFrames(_item.Interval.GetValue(currentFrame, totalFrames, Hz)));
                int repeatCountInt = (int)Math.Round(_item.RepeatCount.GetValue(currentFrame, totalFrames, Hz));
                bool infinite = repeatCountInt <= 0;
                long loopEndFrame = infinite ? long.MaxValue : (long)repeatCountInt * intervalFrames;

                // ── 出力フレーム → ソースフレーム のマッピング ─────────────
                long sourceFrame;
                long framesUntilBoundary;

                if (infinite || currentFrame < loopEndFrame)
                {
                    // ループ中：interval の中での位置を開始オフセット基点にマッピング
                    long offsetInLoop = currentFrame % intervalFrames;
                    sourceFrame = startOffsetFrames + offsetInLoop;

                    long toEndOfInterval = intervalFrames - offsetInLoop;
                    framesUntilBoundary = infinite
                        ? toEndOfInterval
                        : Math.Min(toEndOfInterval, loopEndFrame - currentFrame);
                }
                else
                {
                    // ループ終了後：interval の直後から通常再生
                    long pastLoopFrames = currentFrame - loopEndFrame;
                    sourceFrame = startOffsetFrames + intervalFrames + pastLoopFrames;
                    framesUntilBoundary = long.MaxValue; // 以降は境界なし
                }

                // ── このチャンクで読む float 数 ───────────────────────────
                long maxFrames = framesUntilBoundary == long.MaxValue
                    ? (long)(remaining / Channels)
                    : Math.Min(framesUntilBoundary, (long)(remaining / Channels));
                int toReadFloats = (int)(maxFrames * Channels);

                if (toReadFloats <= 0)
                {
                    // 境界ぴったりで終わった等の安全弁
                    Array.Clear(buffer, offset + written, remaining);
                    written += remaining;
                    break;
                }

                // ── Input をソース位置にシーク（必要な場合のみ）─────────────
                long expectedInputPos = sourceFrame * Channels;
                if (Input.Position != expectedInputPos)
                    Input.Seek(expectedInputPos);

                // ── Input から読み込む ────────────────────────────────────
                int read = Input.Read(buffer, offset + written, toReadFloats);
                if (read <= 0)
                {
                    // ソース末端 → 残りを無音で埋めて終了
                    Array.Clear(buffer, offset + written, remaining);
                    written += remaining;
                    break;
                }

                written += read;
            }

            return written;
        }

        // ────────────────────────────────────────────────────────────────────
        // ヘルパー：出力 float 位置 → ソース float 位置
        // ────────────────────────────────────────────────────────────────────
        long MapOutputToSourcePos(long outputPos)
        {
            long currentFrame = outputPos / Channels;
            long totalFrames = Duration / Channels;

            long startOffsetFrames = MsToFrames(_item.StartOffset.GetValue(currentFrame, totalFrames, Hz));
            long intervalFrames = Math.Max(1L, MsToFrames(_item.Interval.GetValue(currentFrame, totalFrames, Hz)));
            int repeatCountInt = (int)Math.Round(_item.RepeatCount.GetValue(currentFrame, totalFrames, Hz));
            bool infinite = repeatCountInt <= 0;
            long loopEndFrame = infinite ? long.MaxValue : (long)repeatCountInt * intervalFrames;

            long sourceFrame;
            if (infinite || currentFrame < loopEndFrame)
            {
                sourceFrame = startOffsetFrames + (currentFrame % intervalFrames);
            }
            else
            {
                sourceFrame = startOffsetFrames + intervalFrames + (currentFrame - loopEndFrame);
            }

            return sourceFrame * Channels;
        }

        long MsToFrames(double ms) => (long)(ms / 1000.0 * Hz);
    }
}