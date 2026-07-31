using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace AtasIndicators
{
    [DisplayName("Order Flow Alert System")]
    [Description("監測極端主動買/賣單被吸收的型態，並觸發視窗與音效警報")]
    public class OrderFlowAlertSystem : Indicator
    {
        private readonly ValueDataSeries _buySignals;
        private readonly ValueDataSeries _sellSignals;

        // ── 警報設定 ────────────────────────────────────────────────
        private bool _useAlert = true;
        private string _alertFile = "alert1"; 
        
        [Display(Name = "啟用彈跳視窗與音效警報", GroupName = "1. 警報設定", Order = 1)]
        public bool UseAlert 
        { 
            get => _useAlert; 
            set { _useAlert = value; RecalculateValues(); } 
        }

        [Display(Name = "音效檔案名稱 (ATAS內建)", GroupName = "1. 警報設定", Order = 2)]
        public string AlertFile 
        { 
            get => _alertFile; 
            set { _alertFile = value; RecalculateValues(); } 
        }

        // ── 策略參數 ────────────────────────────────────────────────
        private int _period = 20;
        private decimal _volumeMultiplier = 2.5m; // 預設 2.5 倍，過濾掉一般雜訊
        private decimal _wickRatio = 0.4m;

        [Display(Name = "歷史比較週期 (N根K棒)", GroupName = "2. 策略參數", Order = 1)]
        public int Period 
        { 
            get => _period; 
            set { _period = Math.Max(1, value); RecalculateValues(); } 
        }

        [Display(Name = "異常爆量倍數", GroupName = "2. 策略參數", Order = 2)]
        public decimal VolumeMultiplier 
        { 
            get => _volumeMultiplier; 
            set { _volumeMultiplier = Math.Max(1.0m, value); RecalculateValues(); } 
        }

        [Display(Name = "影線佔比要求 (0.1~0.9)", GroupName = "2. 策略參數", Order = 3)]
        public decimal WickRatio 
        { 
            get => _wickRatio; 
            set { _wickRatio = Math.Max(0.1m, Math.Min(0.9m, value)); RecalculateValues(); } 
        }

        public OrderFlowAlertSystem() : base(true)
        {
            _buySignals = new ValueDataSeries("Buy Signal")
            {
                Color = Colors.Cyan,
                VisualType = VisualMode.UpArrow,
                Width = 4
            };

            _sellSignals = new ValueDataSeries("Sell Signal")
            {
                Color = Colors.Magenta,
                VisualType = VisualMode.DownArrow,
                Width = 4
            };

            DataSeries.Add(_buySignals);
            DataSeries.Add(_sellSignals);
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar < _period) return;

            var candle = GetCandle(bar);
            if (candle.Ask == 0 && candle.Bid == 0) return;

            _buySignals[bar] = 0;
            _sellSignals[bar] = 0;

            decimal sumBid = 0;
            decimal sumAsk = 0;
            for (int i = 1; i <= _period; i++)
            {
                var pastCandle = GetCandle(bar - i);
                sumBid += pastCandle.Bid;
                sumAsk += pastCandle.Ask;
            }
            decimal avgBid = sumBid / _period;
            decimal avgAsk = sumAsk / _period;

            decimal range = candle.High - candle.Low;
            if (range == 0) return;

            decimal upperWick = candle.High - Math.Max(candle.Open, candle.Close);
            decimal lowerWick = Math.Min(candle.Open, candle.Close) - candle.Low;

            bool isExtremeBid = candle.Bid >= (avgBid * _volumeMultiplier);
            bool isLowerWickLong = (lowerWick / range) >= _wickRatio;
            bool isBuySignal = isExtremeBid && isLowerWickLong;

            bool isExtremeAsk = candle.Ask >= (avgAsk * _volumeMultiplier);
            bool isUpperWickLong = (upperWick / range) >= _wickRatio;
            bool isSellSignal = isExtremeAsk && isUpperWickLong;

            // 繪製圖表箭頭
            if (isBuySignal) _buySignals[bar] = candle.Low - (15 * InstrumentInfo.TickSize);
            if (isSellSignal) _sellSignals[bar] = candle.High + (15 * InstrumentInfo.TickSize);

            // ── 警報觸發邏輯 ──
            // 只在最新的一根剛「收盤」的 K 棒觸發警報，避免歷史數據瘋狂彈出警報
            if (bar == CurrentBar - 1 && _useAlert)
            {
                if (isBuySignal)
                {
                    AddAlert(_alertFile, $"做多機會！極端空軍砸盤被吸收 (Bid: {candle.Bid})");
                }
                else if (isSellSignal)
                {
                    AddAlert(_alertFile, $"做空機會！極端多軍追高被吸收 (Ask: {candle.Ask})");
                }
            }
        }
    }
}
