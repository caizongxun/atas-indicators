using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace AtasIndicators
{
    [DisplayName("Footprint Delta Analyzer")]
    [Description("顯示每根K棒的Delta、主動買賣量，並標記Delta背離")]
    public class FootprintDeltaAnalyzer : Indicator
    {
        // ── Data Series ────────────────────────────────────────────
        private readonly ValueDataSeries _delta;
        private readonly ValueDataSeries _askVolume;
        private readonly ValueDataSeries _bidVolume;
        private readonly ValueDataSeries _cumulativeDelta;
        private readonly PaintbarsDataSeries _paintBars;

        // ── 參數 ────────────────────────────────────────────────────
        private int _divergenceThreshold = 3;

        [Display(Name = "Delta背離連續K棒數", GroupName = "設定", Order = 1)]
        public int DivergenceThreshold
        {
            get => _divergenceThreshold;
            set
            {
                if (value < 1) return;
                _divergenceThreshold = value;
                RecalculateValues();
            }
        }

        private Color _bullishColor = Colors.LimeGreen;
        private Color _bearishColor = Colors.OrangeRed;
        private Color _divergenceColor = Colors.Yellow;

        [Display(Name = "主動買主色", GroupName = "顏色", Order = 2)]
        public Color BullishColor
        {
            get => _bullishColor;
            set { _bullishColor = value; RecalculateValues(); }
        }

        [Display(Name = "主動賣主色", GroupName = "顏色", Order = 3)]
        public Color BearishColor
        {
            get => _bearishColor;
            set { _bearishColor = value; RecalculateValues(); }
        }

        [Display(Name = "背離警示色", GroupName = "顏色", Order = 4)]
        public Color DivergenceColor
        {
            get => _divergenceColor;
            set { _divergenceColor = value; RecalculateValues(); }
        }

        // ── Constructor ─────────────────────────────────────────────
        public FootprintDeltaAnalyzer() : base(true)
        {
            _delta = new ValueDataSeries("Delta")
            {
                Color      = Colors.DeepSkyBlue,
                Width      = 2,
                VisualType = VisualMode.Line
            };

            _askVolume = new ValueDataSeries("Ask Volume")
            {
                Color      = Colors.LimeGreen,
                VisualType = VisualMode.Histogram
            };

            _bidVolume = new ValueDataSeries("Bid Volume")
            {
                Color      = Colors.OrangeRed,
                VisualType = VisualMode.Histogram
            };

            _cumulativeDelta = new ValueDataSeries("Cumulative Delta (CVD)")
            {
                Color      = Colors.White,
                Width      = 1,
                VisualType = VisualMode.Line
            };

            _paintBars = new PaintbarsDataSeries("Bar Color");

            DataSeries.Add(_delta);
            DataSeries.Add(_askVolume);
            DataSeries.Add(_bidVolume);
            DataSeries.Add(_cumulativeDelta);
            DataSeries.Add(_paintBars);
        }

        // ── 計算邏輯 ────────────────────────────────────────────────
        protected override void OnCalculate(int bar, decimal value)
        {
            var candle = GetCandle(bar);

            decimal askVol = candle.Ask;
            decimal bidVol = candle.Bid;
            decimal delta  = candle.Delta;

            _askVolume[bar]  = askVol;
            _bidVolume[bar]  = -bidVol;
            _delta[bar]      = delta;

            _cumulativeDelta[bar] = bar == 0
                ? delta
                : _cumulativeDelta[bar - 1] + delta;

            _paintBars[bar] = delta switch
            {
                > 0 => _bullishColor,
                < 0 => _bearishColor,
                _   => Colors.Gray
            };

            // ── Delta 背離偵測 ──────────────────────────────────────
            if (bar >= _divergenceThreshold)
            {
                var currentCandle = GetCandle(bar);
                var pastCandle    = GetCandle(bar - _divergenceThreshold);

                // 看跌背離：價格創新高 但 Delta 連續遞減
                bool priceMakingHighs = currentCandle.High > pastCandle.High;
                bool deltaWeakening   = true;
                for (int i = 1; i <= _divergenceThreshold; i++)
                {
                    if (_delta[bar - i + 1] >= _delta[bar - i])
                    { deltaWeakening = false; break; }
                }
                if (priceMakingHighs && deltaWeakening)
                    _paintBars[bar] = _divergenceColor;

                // 看漲背離：價格創新低 但 Delta 連續遞增
                bool priceMakingLows  = currentCandle.Low < pastCandle.Low;
                bool deltaStrengening = true;
                for (int i = 1; i <= _divergenceThreshold; i++)
                {
                    if (_delta[bar - i + 1] <= _delta[bar - i])
                    { deltaStrengening = false; break; }
                }
                if (priceMakingLows && deltaStrengening)
                    _paintBars[bar] = _divergenceColor;
            }
        }
    }
}
