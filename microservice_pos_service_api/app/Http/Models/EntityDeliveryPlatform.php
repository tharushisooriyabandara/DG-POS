<?php
namespace App\Http\Models;

use Illuminate\Database\Eloquent\Model;

class EntityDeliveryPlatform extends Model
{
    /**
     * The table associated with the model.
     *
     * @var string
     */
    protected $table = 'entity_delivery_platform';
    protected $guarded = [];

    public function item()
    {
        return $this->belongsTo('App\Http\Models\Item', 'entity_id');
    }

    public function prices()
    {
        return $this->hasMany('App\Http\Models\ItemPrice', 'entity_item_id', 'entity_id');
    }

    public function modifiers()
    {
        return $this->hasMany('App\Http\Models\ModifierGroupItem', 'item_id', 'external_item_id');
    }

    public function modifierGroupModifierItems()
    {
        return $this->hasMany('App\Http\Models\ModifierGroupModifierItem', 'item_id', 'external_item_id');
    }

    public function modifierList($dp)
    {
        if (count($this->modifiers)>0) {
            $modifierOptions = [];
            $addedModifiers = [];
            foreach ($this->modifiers as $ekey => $entityModifier) {
                foreach ($entityModifier->modifier->items->where('platform', $dp) as $key => $mod) {
                    $mod = $mod->fresh(['modifier', 'item']);
                    if (!is_null($mod->item)/* && count($mod->item->prices->where('delivery_platform_id', $dp))>0*/) {
                        if (!isset($modifierOptions[$mod->modifier_group_id]['modifier'])) {
                            unset($mod->modifier->created_at);
                            unset($mod->modifier->updated_at);
                            $modifierOptions[$mod->modifier_group_id]['modifier'] = $mod->modifier;
                            $modifierOptions[$mod->modifier_group_id]['items'] = [];
                        }
                        if (!isset($addedModifiers[$mod->modifier_group_id]) || !in_array($mod->item_id, $addedModifiers[$mod->modifier_group_id])) {
                            $modItem = $mod->entityItem;
                            $modItemList = $mod->entityItem->modifierList($dp);

                            $modItem->price = number_format(($mod->price==0?0:$mod->price/100), 2, '.', '');
                            
                            $modItemListNew = [];
                            foreach ($modItemList as $key1 => $modItemEl) {
                                $itemArray = [];
                                foreach ($modItemEl["items"] as $key2 => $modItmSingle) {
                                    $modGrpModItm = $modItmSingle->modifierGroupModifierItems->where('platform', $dp)->where('modifier_group_id', $modItemEl['modifier']->id);
                                    if (count($modGrpModItm)>0) {
                                        $modItmSingle->price = number_format(($modGrpModItm->first()->price==0?0:$modGrpModItm->first()->price/100), 2, '.', '');
                                    }
                                    unset($modItmSingle->modifierGroupModifierItems);
                                    $itemArray[] = $modItmSingle;
                                }
                                $modItemEl['items'] = $itemArray;
                                $modItemListNew[] = $modItemEl;
                            }
                            $modItem->modifier_list = $modItemListNew;
                            unset($modItem->modifiers);
                            unset($modItem->created_at);
                            unset($modItem->updated_at);
                            $modifierOptions[$mod->modifier_group_id]['items'][] = $modItem;
                            $addedModifiers[$mod->modifier_group_id][] = $mod->item_id;
                        }
                    }
                }
            }
            return array_values($modifierOptions);
        }
        return [];
    }
}
