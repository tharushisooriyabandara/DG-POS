package generate

import (
	"bytes"
	"encoding/json"
	"fmt"
	"math/rand"
	"os/exec"
	"time"

	"github.com/Delivergate-Dev/pos-service-golang/logger"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"go.uber.org/zap"
)

func RemoteOrderID(itemsCount int) string {
	randomStr := RandomString(12)
	date := time.Now().Format("020106") // DDMMYY
	return fmt.Sprintf("%s-%02d-%s-%s-%s", randomStr[:2], itemsCount, date, randomStr[2:8], randomStr[8:])
}

func RandomString(length int) string {
	const charset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
	b := make([]byte, length)
	for i := range b {
		b[i] = charset[rand.Intn(len(charset))]
	}
	return string(b)
}

func PaymentModeString(orderPayments []*types.OrderPayment) string {
	if len(orderPayments) == 0 {
		return ""
	}

	if len(orderPayments) > 1 {
		return "SPLIT"
	}

	return orderPayments[0].PaymentMethod
}

func SerializedVoucherString(vouchers []*types.OrderVoucher) string {

	if len(vouchers) == 0 {
		vouchers = []*types.OrderVoucher{}
	}

	// convert to json
	vouchersJSON, err := json.Marshal(vouchers)
	if err != nil {
		return ""
	}
	// php serialize
	serialized, err := PhpSerialize(vouchersJSON)
	if err != nil {
		return ""
	}
	return serialized
}

func VouchersFromSerializedString(serialized string) []types.OrderVoucher {
	vouchers := []types.OrderVoucher{}
	if serialized == "" || serialized == "N;" || serialized == "a:0:{}" {
		return nil
	}

	vouchersJSON, err := PhpUnserialize(serialized)
	if err != nil {
		logger.Error("Failed to unserialize vouchers", zap.Error(err))
		return nil
	}

	err = json.Unmarshal(vouchersJSON, &vouchers)
	if err != nil {
		logger.Error("Failed to unmarshal vouchers", zap.Error(err))
		return nil
	}

	return vouchers
}

type modifier struct {
	ItemID        string  `json:"item_id"`
	Title         string  `json:"title"`
	SelectedItems []*item `json:"selected_item"`
	RemovedItems  []*item `json:"removed_item"`
}

type item struct {
	ItemID        string      `json:"item_id"`
	Title         string      `json:"title"`
	Quantity      string      `json:"quantity"`
	PricePerItem  string      `json:"price_per_item"`
	OriginalPrice string      `json:"original_price"`
	DisplayPrice  string      `json:"display_price"`
	TaxDetails    *taxDetails `json:"tax_details"`
	Modifiers     []*modifier `json:"modifiers"`
}

type taxDetails struct {
	TaxID     int32   `json:"tax_id"`
	TaxRate   float64 `json:"tax_rate"`
	TaxCode   string  `json:"tax_code"`
	TaxAmount float64 `json:"tax_amount"`
}

func ModifierString(modifierDetails []*types.ModifierDetails) (string, error) {

	// map modifier details to modifier struct
	modifiers := []*modifier{}
	for _, md := range modifierDetails {
		modifiers = append(modifiers, toModifier(md))
	}

	// fold modifiers by item id and combine selected and removed items
	acc := foldModifiers(modifiers)

	// marshal modifiers to json
	modifiersJSON, err := json.Marshal(acc)
	if err != nil {
		return "", fmt.Errorf("failed to marshal modifiers: %w", err)
	}

	serialized, err := PhpSerialize(modifiersJSON)
	if err != nil {
		logger.Error("Failed to serialize modifiers", zap.Error(err))
		return "", err
	}

	return serialized, nil
}

func toModifier(md *types.ModifierDetails) *modifier {
	var td *taxDetails
	if md.ModifierItem.TaxDetails != nil {
		td = &taxDetails{
			TaxID:     md.ModifierItem.TaxDetails.TaxID,
			TaxRate:   md.ModifierItem.TaxDetails.TaxRate,
			TaxCode:   md.ModifierItem.TaxDetails.TaxCode,
			TaxAmount: md.ModifierItem.TaxDetails.Amount,
		}
	}

	return &modifier{
		ItemID:       fmt.Sprintf("%d", md.ModifierMainItem),
		Title:        md.ModifierGroupName,
		RemovedItems: []*item{},
		SelectedItems: []*item{
			{
				ItemID:        md.ModifierItem.ExternalItemID,
				Title:         md.ModifierItem.ItemName,
				Quantity:      fmt.Sprintf("%.2f", float64(md.Quantity)),
				PricePerItem:  fmt.Sprintf("%.2f", float64(md.ModifierItem.Price)*100),
				OriginalPrice: fmt.Sprintf("%.2f", float64(md.ModifierItem.OriginalPrice)*100),
				DisplayPrice:  fmt.Sprintf("%.2f", md.ModifierItem.Price),
				TaxDetails:    td,
				Modifiers:     nestedModifiers(md.Modifiers),
			},
		},
	}
}

func nestedModifiers(nested []*types.NestedModifier) []*modifier {
	modifiers := []*modifier{}
	for _, nm := range nested {
		modifiers = append(modifiers, toModifier(&types.ModifierDetails{
			ModifierGroupName: nm.ModifierMain.Title,
			ModifierMainItem:  nm.ModifierMain.Id,
			Quantity:          nm.ModifierMain.Quantity,
			ModifierItem:      nm.ModifierItem,
		}))
	}
	return modifiers
}

func foldModifiers(modifiers []*modifier) []*modifier {
	acc := []*modifier{}
	for _, m := range modifiers {
		exists := false
		for i, accM := range acc {
			if accM.ItemID == m.ItemID {
				exists = true
				acc[i].SelectedItems = append(acc[i].SelectedItems, m.SelectedItems...)
				acc[i].RemovedItems = append(acc[i].RemovedItems, m.RemovedItems...)
			}
		}
		if !exists {
			acc = append(acc, m)
		}
	}
	return acc
}

func PhpSerialize(jsonBytes json.RawMessage) (string, error) {

	cmd := exec.Command("php", "-r", `
		$input = file_get_contents("php://stdin");
		$array = json_decode($input, true);
		echo serialize($array);
	`)
	cmd.Stdin = bytes.NewReader(jsonBytes)

	output, err := cmd.Output()
	if err != nil {
		return "", fmt.Errorf("failed to execute php command: %w", err)
	}

	return string(output), nil

}

func PhpUnserialize(serialized string) (json.RawMessage, error) {

	cmd := exec.Command("php", "-r", `
		$input = file_get_contents("php://stdin");
		$array = unserialize($input);
		echo json_encode($array);
	`)
	cmd.Stdin = bytes.NewReader([]byte(serialized))

	output, err := cmd.Output()
	if err != nil {
		return nil, fmt.Errorf("failed to execute php command: %w", err)
	}

	return output, nil
}
