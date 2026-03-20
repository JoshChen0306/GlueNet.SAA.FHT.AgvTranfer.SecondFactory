using System.Configuration;

namespace HikAGVWebAPI
{
    #region DB Config
    public class SectionDB : ConfigurationSection
    {
        [ConfigurationProperty(nameof(DBSettings))]
        public DBSettings DBSettings
        {
            get
            {
                return (DBSettings)this[nameof(DBSettings)];
            }
            set
            {
                this[nameof(DBSettings)] = value;
            }
        }
    }

    public class DBSettings : ConfigurationElement
    {
        [ConfigurationProperty(nameof(DBIP), DefaultValue = "127.0.0.1", IsRequired = true)]
        public string DBIP
        {
            get
            {
                return (string)this[nameof(DBIP)];
            }
            set
            {
                this[nameof(DBIP)] = value;
            }
        }

        [ConfigurationProperty(nameof(DBName), DefaultValue = "", IsRequired = true)]
        public string DBName
        {
            get
            {
                return (string)this[nameof(DBName)];
            }
            set
            {
                this[nameof(DBName)] = value;
            }
        }

        [ConfigurationProperty(nameof(UserID), DefaultValue = "mcs", IsRequired = true)]
        public string UserID
        {
            get
            {
                return (string)this[nameof(UserID)];
            }
            set
            {
                this[nameof(UserID)] = value;
            }
        }

        [ConfigurationProperty(nameof(Password), DefaultValue = "Zz123456", IsRequired = true)]
        public string Password
        {
            get
            {
                return (string)this[nameof(Password)];
            }
            set
            {
                this[nameof(Password)] = value;
            }
        }
    }
    #endregion DB Config

    #region Log Config
    public class SectionLog : ConfigurationSection
    {
        [ConfigurationProperty(nameof(LogSettings))]
        public LogSettings LogSettings
        {
            get
            {
                return (LogSettings)this[nameof(LogSettings)];
            }
            set
            {
                this[nameof(LogSettings)] = value;
            }
        }
    }

    public class LogSettings : ConfigurationElement
    {
        [ConfigurationProperty(nameof(LogPath), DefaultValue = "C:\\Logs\\", IsRequired = true)]
        public string LogPath
        {
            get
            {
                return (string)this[nameof(LogPath)];
            }
            set
            {
                this[nameof(LogPath)] = value;
            }
        }

        [ConfigurationProperty(nameof(KeepDate), DefaultValue = "60")]
        public int KeepDate
        {
            get
            {
                return (int)this[nameof(KeepDate)];
            }
            set
            {
                this[nameof(KeepDate)] = value;
            }
        }
    }
    #endregion Log Config

    #region FHt Config
    public class SectionFHt : ConfigurationSection
    {
        [ConfigurationProperty(nameof(FHtSettings))]
        public FHtSettings FHtSettings
        {
            get
            {
                return (FHtSettings)this[nameof(FHtSettings)];
            }
            set
            {
                this[nameof(FHtSettings)] = value;
            }
        }
    }

    public class FHtSettings : ConfigurationElement
    {
        [ConfigurationProperty(nameof(ContentType), DefaultValue = "application/x-www-form-urlencoded", IsRequired = true)]
        public string ContentType
        {
            get
            {
                return (string)this[nameof(ContentType)];
            }
            set
            {
                this[nameof(ContentType)] = value;
            }
        }

        [ConfigurationProperty(nameof(WebURL), DefaultValue = "http://172.16.10.21//API//TeamService.ashx?ask=postMessage", IsRequired = true)]
        public string WebURL
        {
            get
            {
                return (string)this[nameof(WebURL)];
            }
            set
            {
                this[nameof(WebURL)] = value;
            }
        }

        [ConfigurationProperty(nameof(Teamcode), DefaultValue = "291", IsRequired = true)]
        public string Teamcode
        {
            get
            {
                return (string)this[nameof(Teamcode)];
            }
            set
            {
                this[nameof(Teamcode)] = value;
            }
        }

        [ConfigurationProperty(nameof(APIAccount), DefaultValue = "va_2fd8dfef3c1d4c5293", IsRequired = true)]
        public string APIAccount
        {
            get
            {
                return (string)this[nameof(APIAccount)];
            }
            set
            {
                this[nameof(APIAccount)] = value;
            }
        }

        [ConfigurationProperty(nameof(APIKey), DefaultValue = "fd337716-fb34-4ee8-9f3a-55aec9a37fad", IsRequired = true)]
        public string APIKey
        {
            get
            {
                return (string)this[nameof(APIKey)];
            }
            set
            {
                this[nameof(APIKey)] = value;
            }
        }

        [ConfigurationProperty(nameof(LowBattery), DefaultValue = "40,25", IsRequired = true)]
        public string LowBattery
        {
            get
            {
                return (string)this[nameof(LowBattery)];
            }
            set
            {
                this[nameof(LowBattery)] = value;
            }
        }

        [ConfigurationProperty(nameof(TestMode), DefaultValue = "false", IsRequired = false)]
        public string TestMode
        {
            get
            {
                return (string)this[nameof(TestMode)];
            }
            set
            {
                this[nameof(TestMode)] = value;
            }
        }
    }
    #endregion Log Config

    #region Elevator Config
    public class SectionElevator : ConfigurationSection
    {
        [ConfigurationProperty(nameof(ElevatorSettings))]
        public ElevatorSettings ElevatorSettings
        {
            get { return (ElevatorSettings)this[nameof(ElevatorSettings)]; }
            set { this[nameof(ElevatorSettings)] = value; }
        }
    }

    public class ElevatorSettings : ConfigurationElement
    {
        [ConfigurationProperty(nameof(CustomerElevatorFloors), DefaultValue = "1F,2F,3F")]
        public string CustomerElevatorFloors
        {
            get { return (string)this[nameof(CustomerElevatorFloors)]; }
            set { this[nameof(CustomerElevatorFloors)] = value; }
        }

        [ConfigurationProperty(nameof(CustomerElevatorWaitPoints), DefaultValue = "1F:U1,2F:V1,3F:W1")]
        public string CustomerElevatorWaitPoints
        {
            get { return (string)this[nameof(CustomerElevatorWaitPoints)]; }
            set { this[nameof(CustomerElevatorWaitPoints)] = value; }
        }

        [ConfigurationProperty(nameof(CustomerElevatorInsidePoints), DefaultValue = "1F:U2,2F:V2,3F:W2")]
        public string CustomerElevatorInsidePoints
        {
            get { return (string)this[nameof(CustomerElevatorInsidePoints)]; }
            set { this[nameof(CustomerElevatorInsidePoints)] = value; }
        }

        [ConfigurationProperty(nameof(FreightElevatorFloors), DefaultValue = "3F,4F")]
        public string FreightElevatorFloors
        {
            get { return (string)this[nameof(FreightElevatorFloors)]; }
            set { this[nameof(FreightElevatorFloors)] = value; }
        }

        [ConfigurationProperty(nameof(FreightElevatorWaitPoints), DefaultValue = "3F:X1,4F:Y1")]
        public string FreightElevatorWaitPoints
        {
            get { return (string)this[nameof(FreightElevatorWaitPoints)]; }
            set { this[nameof(FreightElevatorWaitPoints)] = value; }
        }

        [ConfigurationProperty(nameof(FreightElevatorInsidePoints), DefaultValue = "3F:X2,4F:Y2")]
        public string FreightElevatorInsidePoints
        {
            get { return (string)this[nameof(FreightElevatorInsidePoints)]; }
            set { this[nameof(FreightElevatorInsidePoints)] = value; }
        }

        [ConfigurationProperty(nameof(StationFloorMapping), DefaultValue = "A:1F,B:1F,C:1F,D:1F,E:1F,G:1F,H:2F,I:3F,J:3F,K:4F,L:4F,M:2F,N:2F,O:2F,P:2F,Q:2F,R:2F,S:2F,T:2F")]
        public string StationFloorMapping
        {
            get { return (string)this[nameof(StationFloorMapping)]; }
            set { this[nameof(StationFloorMapping)] = value; }
        }
    }
    #endregion Elevator Config
}