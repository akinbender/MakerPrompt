using System.ComponentModel;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Shared.Models
{
    public class PrinterTelemetry : INotifyPropertyChanged
    {
        private readonly object _lock = new();

        private string _lastResponse = "";
        public string LastResponse
        {
            get => _lastResponse;
            set => SetField(ref _lastResponse, value, nameof(LastResponse));
        }

        private string _printerName = "My 3D Printer";
        public string PrinterName
        {
            get => _printerName;
            set => SetField(ref _printerName, value, nameof(PrinterName));
        }

        private DateTime? _connectionTime;
        public DateTime? ConnectionTime
        {
            get => _connectionTime;
            set => SetField(ref _connectionTime, value, nameof(ConnectionTime));
        }

        private double _hotendTemp;
        public double HotendTemp
        {
            get => _hotendTemp;
            set => SetField(ref _hotendTemp, value, nameof(HotendTemp));
        }

        private double _hotendTarget;
        public double HotendTarget
        {
            get => _hotendTarget;
            set => SetField(ref _hotendTarget, value, nameof(HotendTarget));
        }

        private double _bedTemp;
        public double BedTemp
        {
            get => _bedTemp;
            set => SetField(ref _bedTemp, value, nameof(BedTemp));
        }

        private double _bedTarget;
        public double BedTarget
        {
            get => _bedTarget;
            set => SetField(ref _bedTarget, value, nameof(BedTarget));
        }

        private Vector3 _position = new();
        public Vector3 Position
        {
            get => _position;
            set => SetField(ref _position, value, nameof(Position));
        }

        private PrinterStatus _status = PrinterStatus.Disconnected;
        public PrinterStatus Status
        {
            get => _status;
            set => SetField(ref _status, value, nameof(Status));
        }

        private int _feedRate;
        public int FeedRate
        {
            get => _feedRate;
            set => SetField(ref _feedRate, value, nameof(FeedRate));
        }

        private int _flowRate;
        public int FlowRate
        {
            get => _flowRate;
            set => SetField(ref _flowRate, value, nameof(FlowRate));
        }

        private int _fanSpeed;
        public int FanSpeed
        {
            get => _fanSpeed;
            set => SetField(ref _fanSpeed, value, nameof(FanSpeed));
        }

        public SDCardStatus SDCard { get; } = new();


        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            lock (_lock)
            {
                if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                field = value;
                OnPropertyChanged(propertyName);
                return true;
            }
        }
    }

    // Support classes
    public record Vector3(double X, double Y, double Z)
    {
        public Vector3() : this(0, 0, 0) { }
    }

    public class SDCardStatus
    {
        public bool Present { get; set; }
        public bool Printing { get; set; }
        public double Progress { get; set; } // 0-100%
    }
}
