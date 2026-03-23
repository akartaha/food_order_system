using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace food_order_system1.Modles
{
    public class EmailSetting
    {
    public string SmtpHost { get; set; }=string.Empty;
    public int SmtpPort { get; set; }
    public string SmtpUser { get; set; }=string.Empty;
    public string SmtpPass { get; set; }=string.Empty;
    public string FromEmail { get; set; }=string.Empty;
    }
}