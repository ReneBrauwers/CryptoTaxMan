using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public static class DateTimeExtensions
    {

        public static DateTime FromSpecifiedToUTC(this DateTime fromDateTime, string fromZoneId)
        {


            LocalDateTime fromLocal = LocalDateTime.FromDateTime(fromDateTime);
            DateTimeZone fromZone = DateTimeZoneProviders.Tzdb[fromZoneId];
            ZonedDateTime fromZoned = fromLocal.InZoneLeniently(fromZone);

            //DateTimeZone toZone = DateTimeZoneProviders.Tzdb["Etc/UTC"];
            //ZonedDateTime toZoned = fromZoned.WithZone(toZone);
            //LocalDateTime toLocal = toZoned.LocalDateTime;
            return fromZoned.ToDateTimeUtc();


        }

        public static DateTime FromUTCToSpecified(this DateTime fromUTCDateTime, string toZoneId)
        {
            var timeZone = DateTimeZoneProviders.Tzdb[toZoneId];
            // the date as UTC - this could be from a data store

            // convert to instant from UTC - see http://stackoverflow.com/questions/20807799/using-nodatime-how-to-convert-an-instant-to-the-corresponding-systems-zoneddat
            var instant = Instant.FromDateTimeUtc(DateTime.SpecifyKind(fromUTCDateTime, DateTimeKind.Utc));
            return instant.InZone(timeZone).ToDateTimeUnspecified();



        }
 
    }
}
