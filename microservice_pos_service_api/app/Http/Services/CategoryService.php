<?php

namespace App\Http\Services;

use Exception;
use App\Http\Models\Category;
use App\Http\Models\PosCategory;
use App\Http\Models\CategoryMenu;
use App\Http\Models\ItemCategory;
use App\Http\Services\PosService;
use App\Http\Helpers\CommonHelper;
use Illuminate\Support\Facades\DB;
use App\microservice_delivergate_api\Services\BaseService as BaseService;

class CategoryService extends BaseService
{
    public function getCategories($main_menu, $query = null, $bogo = false)
    {
        try {
            if (is_null($main_menu)) {
                $categories = Category::orderBy('priority');
            } else {
                $catIds = CategoryMenu::where('main_menu_id', $main_menu)->pluck('category_id')->toArray();
                $categories = Category::whereIn('id', $catIds)->orderBy('priority');
            }
            if ($bogo) {
                $categories = $categories->where('is_bogo_category', 1);
            }
            if (is_null($query)) {
                $categories = $categories->get();
            } else {
                $categories = $categories->where('title', 'LIKE', '%'.$query.'%')->get();
            }
            return $this->success('Categories', $categories);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function store($data)
    {
        try {
            $category = new Category;
            if (isset($data['id'])) {
                $category->id = $data['id'];
            }
            $category->remote_id = (isset($data['remote_id']) ? $data['remote_id'] : null);
            $category->title = $data['title'];
            $category->sub_title = (isset($data['sub_title']) ? $data['sub_title'] : null);
            $category->image_path = (isset($data['image_path']) ? $data['image_path'] : null);
            $category->parent_id = (isset($data['parent_id']) ? $data['parent_id'] : 0);
            $category->description = (isset($data['description']) ? $data['description'] : null);
            $category->status = $data['status'];
            $category->is_bogo_category = (isset($data['is_bogo_category']) ? $data['is_bogo_category'] : 0);
            $category->buy_quantity = (isset($data['buy_quantity']) ? $data['buy_quantity'] : 0);
            $category->get_quantity = (isset($data['get_quantity']) ? $data['get_quantity'] : 0);
            DB::transaction(function () use ($category, $data) {
                $category->save();
                $input = ['name' => $category->title, 'category_id' => $category->id];
                if (isset($data['items'])) {
                    if (isset($data['main_menu'])) {
                        $catItems = [];
                        $itemIds = array_unique($data['items']);
                        foreach ($itemIds as $key => $itm) {
                            $catItems[] = ['category_id' => $category->id, 'item_id' => $itm, 'main_menu_id' => $data['main_menu']];
                        }
                        $category->items()->detach();
                        $category->items()->attach($catItems);
                    } else {
                        $catItems = [];
                        $itemIds = array_unique($data['items']);
                        foreach ($itemIds as $key => $itm) {
                            $catItems[] = ['category_id' => $category->id, 'item_id' => $itm, 'main_menu_id' => null];
                        }
                        $category->items()->detach();
                        $category->items()->attach($catItems);
                    }
                }
                if (!isset($data['remote_id'])) {
                    try {
                        $posService = new PosService;
                        $response = $posService->createRemoteCategory($input);
                    } catch (Exception $e) {
                        $this->loggerError($e, $this, __FUNCTION__, __LINE__);
                    }
                }
                CommonHelper::userLog(null, ['description' => 'Created category titled "' . $category->title . '"', 'event' => 'create', 'subject_type' => 'category', 'subject_id' => $category->id]);
            });
            return $this->success('Successfully created the category.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function show($id)
    {
        try {
            $category = Category::find($id);
            if (is_null($category)) {
                return $this->notFound('Category not found');
            }
            return $this->success('Category', $category);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function update($data, $id)
    {
        try {
            if (isset($data['loyverse'])) {
                $category = Category::where('remote_id', $id)->first();
            } else {
                $category = Category::find($id);
            }
            $category->title = $data['title'];
            $category->sub_title = (isset($data['sub_title']) ? $data['sub_title'] : null);
            $category->image_path = (isset($data['image_path']) ? $data['image_path'] : null);
            $category->parent_id = (isset($data['parent_id']) ? $data['parent_id'] : 0);
            $category->description = (isset($data['description']) ? $data['description'] : null);
            $category->status = $data['status'];
            $category->is_bogo_category = (isset($data['is_bogo_category']) ? $data['is_bogo_category'] : 0);
            $category->buy_quantity = (isset($data['buy_quantity']) ? $data['buy_quantity'] : 0);
            $category->get_quantity = (isset($data['get_quantity']) ? $data['get_quantity'] : 0);
            DB::transaction(function () use ($category, $data) {
                $category->save();
                $input = ['name' => $category->title, 'category_id' => $category->id, 'type' => 'UPDATE'];
                if (isset($data['main_menu'])) {
                    if (isset($data['items'])) {
                        ItemCategory::where('main_menu_id', $data['main_menu'])->where('category_id', $category->id)->delete();
                        $catItems = [];
                        $itemIds = array_unique($data['items']);
                        foreach ($itemIds as $key => $itm) {
                            $catItems[] = ['category_id' => $category->id, 'item_id' => $itm, 'main_menu_id' => $data['main_menu']];
                        }
                        $category->items()->detach();
                        $category->items()->attach($catItems);
                    }
                } elseif (isset($data['items'])) {
                    $catItems = [];
                    ItemCategory::where('category_id', $category->id)->whereNull('main_menu_id')->delete();
                    $itemIds = array_unique($data['items']);
                    foreach ($itemIds as $key => $itm) {
                        $catItems[] = ['category_id' => $category->id, 'item_id' => $itm, 'main_menu_id' => null];
                    }
                    $category->items()->detach();
                    $category->items()->attach($catItems);
                }
                if (!isset($data['loyverse'])) {
                    try {
                        $posService = new PosService;
                        $response = $posService->createRemoteCategory($input);
                    } catch (Exception $e) {
                        $this->loggerError($e, $this, __FUNCTION__, __LINE__);
                    }
                }
                CommonHelper::userLog(null, ['description' => 'Updated category', 'event' => 'update', 'subject_type' => 'category', 'subject_id' => $category->id]);
            });
            return $this->success('Successfully updated the category.');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function categoryItems($id, $main_menu)
    {
        try {
            $category = Category::find($id);
            if (is_null($category)) {
                return $this->notFound('Category not found');
            }
            $category->itemList = $category->itemList($main_menu);
            return $this->success('Category items', $category->itemList);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function categoryMenus($id)
    {
        try {
            $category = Category::find($id);
            if (is_null($category)) {
                return $this->notFound('Category not found');
            }
            return $this->success('Category menus', $category->menus);
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function destroy($id, $main_menu)
    {
        try {
            $category = Category::find($id);
            if (is_null($category)) {
                return $this->notFound('Category not found');
            }
            DB::transaction(function () use ($category, $id, $main_menu) {
                if (is_null($main_menu)) {
                    CategoryMenu::where('category_id', $id)->delete();
                    ItemCategory::where('category_id', $id)->delete();
                    PosCategory::where('category_id', $id)->delete();
                    $category->delete();
                    CommonHelper::userLog(null, ['description' => 'Deleted category titled "' . $category->title . '"', 'event' => 'delete', 'subject_type' => 'category', 'subject_id' => $category->id]);
                } else {
                    CategoryMenu::where('category_id', $id)->where('main_menu_id', $main_menu)->delete();
                    ItemCategory::where('category_id', $id)->where('main_menu_id', $main_menu)->delete();
                }
            });
            return $this->success('Successfully deleted the category');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong!');
        }
    }

    public function storeOrUpdatePosCategory($data)
    {
        try {
            DB::transaction(function () use ($data) {
                $posCategory = PosCategory::firstOrNew([
                    'pos_id' => $data['pos_id'],
                    'remote_id' => $data['remote_id'],
                ]);
                $posCategory->title = $data['title'];
                $posCategory->category_id = $data['category_id'];
                $posCategory->sub_title = $data['sub_title'];
                $posCategory->description = $data['description'];
                $posCategory->parent_id = $data['parent_id'];
                $posCategory->image_path = $data['image_path'];
                $posCategory->status = $data['status'];
                $posCategory->save();
            });
            return $this->success('Category');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }

    public function updateCategoryPriority($data)
    {
        try {
            DB::transaction(function () use ($data) {
                foreach ($data['priority'] as $priority => $categoryId) {
                    $cat = Category::find($categoryId);
                    if (!is_null($cat)) {
                        $cat->priority = $priority;
                        $cat->save();
                    }
                }
            });
            return $this->success('Category priority');
        } catch (Exception $e) {
            $this->loggerError($e, $this, __FUNCTION__, __LINE__);
            return $this->error('Something went wrong');
        }
    }
}
