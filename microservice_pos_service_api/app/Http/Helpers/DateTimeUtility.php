<?php
namespace App\Http\Helpers;

use Illuminate\Support\Facades\Session;
use \DateTime;
use \DateTimeZone;

class DateTimeUtility
{
    public static function getDateTimeFormat($datetime, $format)
    {
        return date($format, strtotime($datetime));
    }

    public static function getTimeFormat($time, $format)
    {
        return date($format, strtotime(date('Y-m-d') . ' ' . $time));
    }

    public static function getTwelveHoursTimeFormat($time)
    {
        return date("h:i a", strtotime($time));
    }

    public static function isDatePassed($fromdate, $todate)
    {
        return strtotime($fromdate) > strtotime($todate) || strtotime($fromdate) == strtotime($todate);
    }

    /*$fromdate = Current time
    $todate = Comparing time*/
    public static function datePassed($fromdate, $todate)
    {
        $fromdate = new DateTime($fromdate);
        $todate = new DateTime($todate);
        return ((strtotime($fromdate->format('Y-m-d H:i:s')) > strtotime($todate->format('Y-m-d H:i:s'))) || (strtotime($fromdate->format('Y-m-d H:i:s')) == strtotime($todate->format('Y-m-d H:i:s'))));
    }

    public static function getDateTimeFormatToUTC($datetime, $format)
    {
        if (Session::has('timezone') && Session::get('timezone') != false) {
            $dt = new DateTime($datetime, new DateTimeZone(Session::get('timezone')));
            $dt->setTimezone(new DateTimeZone('UTC'));
            return $dt->format($format);
        } else {
            $dt = new DateTime($datetime);
            return $dt->format($format);
        }
    }

    public static function getDateTimeFormatToUserTimeZone($datetime, $format)
    {
        $dtm = new DateTime($datetime);
        if ($dtm->format('H:i:s') == '00:00:00' || $dtm->format('H:i:s') == '23:59:59') {
            return $dtm->format($format);
        } else {
            $dt = new DateTime($datetime, new DateTimeZone('UTC'));
            $dt->setTimezone(new DateTimeZone('Asia/Colombo'));
            return $dt->format($format);
        }
    }

    public static function getDateTimeDiffHistoryToUserTimeZone($value)
    {
        $now = new DateTime('NOW', new DateTimeZone('UTC'));
        $datetime = new DateTime($value, new DateTimeZone('UTC'));
        if ($now < $datetime) {
            return false;
        }
        $interval = $now->getTimeStamp() - $datetime->getTimeStamp();

        //$time = time() - $time; // to get the time since that moment
        $time = $interval;
        $time = ($time < 1) ? 1 : $time;
        $tokens = array(
            31536000 => 'year',
            2592000 => 'month',
            604800 => 'week',
            86400 => 'day',
            3600 => 'hour',
            60 => 'minute',
            1 => 'second',
        );

        foreach ($tokens as $unit => $text) {
            if ($time < $unit) {
                continue;
            }
            $numberOfUnits = floor($time / $unit);
            return $numberOfUnits . ' ' . $text . (($numberOfUnits > 1) ? 's' : '');
        }
    }

    public static function getDateTimeDifferenceFormat($fromDate, $toDate, $format)
    {
        $date1 = new DateTime($fromDate);
        $date2 = new DateTime($toDate);
        $interval = $date1->diff($date2);
        return $interval->format($format);
    }

    public static function formatDateTime($date, $format)
    {
        $datetime = DateTime::createFromFormat('d/m/Y', $date);
        return $datetime->format($format);
    }
    
    public static function secondsToDateFormat($numberofsecs, $format)
    {
        return date($format, $numberofsecs);
    }

    public static function addRemoveDaysFromDate($date, $days, $format)
    {
        $datetime = new DateTime($date);
        $datetime->modify($days);
        return $datetime->format($format);
    }

    public static function getDateTimeFormatWithTimeZone($datetime, $format, $timeZone = null)
    {
        $dt = new DateTime($datetime, new DateTimeZone('UTC'));
        $dt->setTimezone(!is_null($timeZone) ? (new DateTimeZone($timeZone)) : new DateTimeZone('Europe/London'));
        return $dt->format($format);
    }

    public static function isTimeBetween($from, $to, $time)
    {
        $time = DateTime::createFromFormat('H:i:s', $time);
        $from = DateTime::createFromFormat('H:i:s', $from.':00');
        $to = DateTime::createFromFormat('H:i:s', $to.':59');
        if ($time > $from && $time < $to) {
           return true;
        } else {
            return false;
        }
    }
}
