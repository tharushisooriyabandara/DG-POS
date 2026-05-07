package convert

import (
	"fmt"
	"strconv"
	"time"
)

func parseAmount(dbValue int32) float64 {
	return float64(dbValue) / 100
}

func parseFloat(dbValue string) float64 {
	value, _ := strconv.ParseFloat(dbValue, 64)
	return value
}

func parseInt(dbValue string) int32 {
	value, _ := strconv.Atoi(dbValue)
	return int32(value)
}

func Uint64ToInt32Slice(uint64Slice []uint64) []int32 {
	int32Slice := make([]int32, len(uint64Slice))
	for i, v := range uint64Slice {
		int32Slice[i] = int32(v)
	}
	return int32Slice
}

func toTimeDurationString(duration time.Duration) string {
	hours := int(duration.Hours())
	minutes := int(duration.Minutes()) % 60
	seconds := int(duration.Seconds()) % 60
	return fmt.Sprintf("%02d:%02d:%02d", hours, minutes, seconds)
}
