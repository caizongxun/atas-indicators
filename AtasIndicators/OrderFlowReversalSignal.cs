using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace AtasIndicators
{
    [DisplayName("Order Flow Reversal Signal")]
    [Description("結合足跡圖 Delta 與 K 棒型態，尋找吸收與套牢現象，提供反轉進出場信號")]
    public class OrderFlowReversalSignal : Indicator
    {
        // ── Data Series ────────────────────────────────────────────
        private readonly ValueDataSeries _buySignals;
        private readonly ValueDataSeries _sellSignals;

        // ── 參數 ────────────────────────────────────────────────────
        private decimal _minVolume = 1000m;
        private decimal _minDeltaAbs = 300m;
        private decimal _wickRatio = 0.5m;

        [Display(Name = "最小觸發成交量 (Volume)", GroupName = "設定", Order = 1)]
        public decimal MinVolume
        {
            get => _minVolume;
            set { _minVolume = Math.Max(1, value); RecalculateValues(); }
        }

        [Display(Name = "最小 Delta 絕對值", GroupName = "設定", Order = 2)]
        public decimal MinDeltaAbs
        {
            get => _minDeltaAbs;
            set { _minDeltaAbs = Math.Max(1, value); RecalculateValues(); }
        }

        [Display(Name = "影線佔K棒比例 (0.1 ~ 0.9)", GroupName = "設定", Order = 3)]
        public decimal WickRatio
        {
            get => _wickRatio;
            set { _wickRatio = Math.Max(0.1m, Math.Min(0.9m, value)); RecalculateValues(); }
        }

        // ── Constructor ─────────────────────────────────────────────
        public OrderFlowReversalSignal() : base(true)
        {
            _buySignals = new ValueDataSeries("Buy Signal")
            {
                Color = Colors.LimeGreen,
                VisualType = VisualMode.UpArrow,
                Width = 3
            };

            _sellSignals = new ValueDataSeries("Sell Signal")
            {
                Color = Colors.Red,
                VisualType = VisualMode.DownArrow,
                Width = 3
            };

            DataSeries.Add(_buySignals);
            DataSeries.Add(_sellSignals);
        }

        // ── 計算邏輯 ────────────────────────────────────────────────
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0) return;

            var candle = GetCandle(bar);

            // 防呆：如果沒有足跡數據 (Ask 和 Bid 均為 0)，則無法計算 Delta，跳過邏輯
            if (candle.Ask == 0 && candle.Bid == 0) return;

            // 初始化當前 K 棒的信號
            _buySignals[bar] = 0;
            _sellSignals[bar] = 0;

            decimal range = candle.High - candle.Low;
            if (range == 0) return;

            decimal volume = candle.Volume;
            decimal delta = candle.Delta;

            // 計算上下影線長度
            decimal upperWick = candle.High - Math.Max(candle.Open, candle.Close);
            decimal lowerWick = Math.Min(candle.Open, candle.Close) - candle.Low;

            // ── 做多信號：吸收型態 (大量主動賣單被吸收，留下長下影線) ──
            bool isHighVolume = volume >= _minVolume;
            bool isHeavySelling = delta <= -_minDeltaAbs;
            bool isLongLowerWick = (lowerWick / range) >= _wickRatio;

            if (isHighVolume && isHeavySelling && isLongLowerWick)
            {
                // 將箭頭畫在 K 棒低點下方，根據 TickSize 調整距離確保清晰
                _buySignals[bar] = candle.Low - (10 * InstrumentInfo.TickSize);
            }

            // ── 做空信號：衰竭/套牢型態 (大量主動買單被套牢，留下長上影線) ──
            bool isHeavyBuying = delta >= _minDeltaAbs;
            bool isLongUpperWick = (upperWick / range) >= _wickRatio;

            if (isHighVolume && isHeavyBuying && isLongUpperWick)
            {
                // 將箭頭畫在 K 棒高點上方
                _sellSignals[bar] = candle.High + (10 * InstrumentInfo.TickSize);
            }
        }
    }
}
