<?php

namespace App\Http\Services;

use DB;
use Exception;
use App\Http\Models\TaxMain;
use App\Http\Models\Shop;
use App\Http\Models\TaxRule;
use App\Http\Models\TaxProfile;
use App\Http\Models\TaxConditionType;
use App\Http\Models\TaxRuleCondition;
use App\microservice_delivergate_api\Services\BaseService as BaseService;

class TaxMainService extends BaseService
{
    public function getTaxes($status = null, $query = null)
    {
        try {
            $taxes = TaxMain::query();
            if (!is_null($status)) {
                $taxes->where('status', $status);
            }
            if (!is_null($query)) {
                $taxes->where('name', 'like', '%' . $query . '%')
                      ->orWhere('code', 'like', '%' . $query . '%');
            }
            $taxes = $taxes->paginate(10);
            $data = ['currentPage' => $taxes->currentPage(), 'lastPage' => $taxes->lastPage(), 'nextPage' => (($taxes->currentPage() == $taxes->lastPage()) ? null : ($taxes->currentPage() + 1)), 'previousPage' => ($taxes->currentPage() == 1 ? null : ($taxes->currentPage() - 1)), 'taxes' => $taxes->items(), 'total' => $taxes->total()];
            return $this->success('Taxes', $data);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function storeTax($data)
    {
        try {
            $existCode = TaxMain::where('code', $data['code'])->first();
            if (!is_null($existCode)) {
                return $this->error('Tax code already exists.');
            }
            DB::transaction(function () use ($data) {
                $tax = new TaxMain;
                $tax->name = $data['name'];
                $tax->code = $data['code'];
                $tax->description = isset($data['description']) ? $data['description'] : null;
                $tax->rate = $data['rate'];
                $tax->status = $data['status'];
                $tax->save();
            });

        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
        return $this->success('Successfully created the Tax.');
    }

    public function getTax($id)
    {
        try {
            $tax = TaxMain::find($id);
            if (is_null($tax)) {
                return $this->notFound('Tax not found');
            }
            return $this->success('Tax', $tax);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function updateTax($data, $id)
    {
        try {
            $existCode = TaxMain::where('code', $data['code'])->where('id', '!=', $id)->first();
            if (!is_null($existCode)) {
                return $this->error('Tax code already exists.');
            }
            DB::transaction(function () use ($data, $id) {
                $tax = TaxMain::find($id);
                $tax->name = $data['name'];
                $tax->code = $data['code'];
                $tax->description = isset($data['description']) ? $data['description'] : null;
                $tax->rate = $data['rate'];
                $tax->status = $data['status'];
                $tax->save();
            });

        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
        return $this->success('Successfully updated the Tax.');
    }

    public function destroyTax($id)
    {
        try {
            TaxMain::find($id)->delete();
            return $this->success('Successfully deleted the Tax.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function getTaxProfiles($status = null, $query = null)
    {
        try {
            $taxProfiles = TaxProfile::query();
            if (!is_null($status)) {
                $taxProfiles->where('status', $status);
            }
            if (!is_null($query)) {
                $taxProfiles->where('name', 'like', '%' . $query . '%');
            }
            $taxProfiles = $taxProfiles->paginate(10);
            $data = ['currentPage' => $taxProfiles->currentPage(), 'lastPage' => $taxProfiles->lastPage(), 'nextPage' => (($taxProfiles->currentPage() == $taxProfiles->lastPage()) ? null : ($taxProfiles->currentPage() + 1)), 'previousPage' => ($taxProfiles->currentPage() == 1 ? null : ($taxProfiles->currentPage() - 1)), 'taxProfiles' => $taxProfiles->items(), 'total' => $taxProfiles->total()];
            return $this->success('Tax Profiles', $data);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function storeTaxProfile($data)
    {
        try {
            DB::transaction(function () use ($data) {
                $taxProfile = new TaxProfile;
                $taxProfile->name = $data['name'];
                $taxProfile->description = isset($data['description']) ? $data['description'] : null;
                $taxProfile->status = $data['status'];
                $taxProfile->save();

                if (!empty($data['tax_rules'])) {
                    foreach ($data['tax_rules'] as $key => $rule) {
                        $taxRule = new TaxRule;
                        $taxRule->tax_profile_id = $taxProfile->id;
                        $taxRule->tax_id = $rule['tax_id'];
                        $taxRule->name = $rule['name'];
                        $taxRule->save();

                        if (!empty($rule['tax_rule_conditions'])) {
                            foreach ($rule['tax_rule_conditions'] as $key => $condition) {
                                $taxRuleCondition = new TaxRuleCondition;
                                $taxRuleCondition->tax_rule_id = $taxRule->id;
                                $taxRuleCondition->condition_type = $condition['condition_type'];
                                $taxRuleCondition->condition_value = isset($condition['condition_value']) ? $condition['condition_value'] : null;
                                $taxRuleCondition->min_value = isset($condition['min_value']) ? $condition['min_value'] : null;
                                $taxRuleCondition->max_value = isset($condition['max_value']) ? $condition['max_value'] : null;
                                $taxRuleCondition->start_date = isset($condition['start_date']) ? $condition['start_date'] : null;
                                $taxRuleCondition->end_date = isset($condition['end_date']) ? $condition['end_date'] : null;
                                $taxRuleCondition->save();
                            }
                        }
                    }
                }
            });

        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
        return $this->success('Successfully created the Tax Profile.');
    }

    public function getTaxProfile($id)
    {
        try {
            $taxProfile = TaxProfile::with('taxRules.tax', 'taxRules.taxRuleConditions')->find($id);

            if (is_null($taxProfile)) {
                return $this->notFound('Tax Profile not found');
            }

            return $this->success('Tax Profile', $taxProfile);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function updateTaxProfile($data, $id)
    {
        try {
            $taxProfile = TaxProfile::with('taxRules.taxRuleConditions')->find($id);
            if (is_null($taxProfile)) {
                return $this->notFound('Tax Profile not found');
            }
            DB::transaction(function () use ($data, $taxProfile) {
                $taxProfile->name = $data['name'];
                $taxProfile->description = isset($data['description']) ? $data['description'] : null;
                $taxProfile->status = $data['status'];
                $taxProfile->save();

                $requestRuleIds = [];
                if (!empty($data['tax_rules'])) {
                    foreach ($data['tax_rules'] as $key => $rule) {
                        $taxRule = TaxRule::updateOrCreate(
                            ['id' => $rule['id'] ?? null],
                            [
                                'tax_profile_id' => $taxProfile->id,
                                'tax_id' => $rule['tax_id'],
                                'name' => $rule['name'],
                            ]
                        );
                        $requestRuleIds[] = $taxRule->id;

                        $requestRuleConditionIds = [];
                        if (!empty($rule['tax_rule_conditions'])) {
                            foreach ($rule['tax_rule_conditions'] as $key => $condition) {
                                $taxRuleCondition = TaxRuleCondition::updateOrCreate(
                                    ['id' => $condition['id'] ?? null],
                                    [
                                        'tax_rule_id' => $taxRule->id,
                                        'condition_type' => $condition['condition_type'],
                                        'condition_value' => isset($condition['condition_value']) ? $condition['condition_value'] : null,
                                        'min_value' => isset($condition['min_value']) ? $condition['min_value'] : null,
                                        'max_value' => isset($condition['max_value']) ? $condition['max_value'] : null,
                                        'start_date' => isset($condition['start_date']) ? $condition['start_date'] : null,
                                        'end_date' => isset($condition['end_date']) ? $condition['end_date'] : null,
                                    ]
                                );
                                $requestRuleConditionIds[] = $taxRuleCondition->id;
                            }
                        }
                        $taxRule->taxRuleConditions()
                        ->whereNotIn('id', $requestRuleConditionIds)
                        ->delete();
                    }
                }
                $taxProfile->taxRules()
                ->whereNotIn('id', $requestRuleIds)
                ->each(function ($rule) {
                    $rule->taxRuleConditions()->delete();
                    $rule->delete();
                });

            });

        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
        return $this->success('Successfully updated the Tax Profile.');
    }

    public function destroyTaxProfile($id)
    {
        try {
            $taxProfile = TaxProfile::with('taxRules.taxRuleConditions')->find($id);
            if (is_null($taxProfile)) {
                return $this->notFound('Tax Profile not found');
            }
            $taxProfile->taxRules()->each(function ($rule) {
                $rule->taxRuleConditions()->delete();
                $rule->delete();
            });
            $taxProfile->delete();
            return $this->success('Successfully deleted the Tax Profile.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function getTaxConditionTypes()
    {
        try {
            $conditionTypes = TaxConditionType::all();

            return $this->success('Tax Condition Types', $conditionTypes);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }
}

