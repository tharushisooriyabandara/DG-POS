package types

type SessionUser struct {
	ID         uint64 `json:"userId"`
	BrandID    int32  `json:"brandId"`
	ShopID     uint64 `json:"shopId"`
	TenantCode string `json:"tenantCode"`
}
