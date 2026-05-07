<?php

namespace App\Http\Controllers;

use App\Http\RequestHandlers\CreateCategoryRequestHandler;
use App\Http\RequestHandlers\UpdateCategoryRequestHandler;
use App\Http\Services\CategoryService;
use Illuminate\Http\Request;

class CategoryController extends Controller
{
    private $category_service;

    public function __construct()
    {
        $this->category_service = new CategoryService;
    }
    /**
     * Display a listing of the resource.
     *
     * @return \Illuminate\Http\Response
     */
    public function index(Request $request)
    {
        $result = $this->category_service->getCategories(($request->has('main_menu') ? $request->get('main_menu') : null), ($request->has('q') ? $request->get('q') : null), ($request->has('bogo') ? $request->get('bogo') : false));
        return $result;
    }

    /**
     * Store a newly created resource in storage.
     *
     * @param  \Illuminate\Http\Request  $request
     * @return \Illuminate\Http\Response
     */
    public function store(CreateCategoryRequestHandler $request)
    {
        $result = $this->category_service->store($request->all());
        return $result;
    }

    /**
     * Display the specified resource.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function show($id)
    {
        $result = $this->category_service->show($id);
        return $result;
    }

    /**
     * Display the items belongs to the category.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function categoryItems($id, Request $request)
    {
        $result = $this->category_service->categoryItems($id, ($request->has('main_menu') ? $request->get('main_menu') : null));
        return $result;
    }

    /**
     * Display the menus belongs to the category.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function categoryMenus($id)
    {
        $result = $this->category_service->categoryMenus($id);
        return $result;
    }

    /**
     * Update the specified resource in storage.
     *
     * @param  \Illuminate\Http\Request  $request
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */
    public function update(UpdateCategoryRequestHandler $request, $id)
    {
        $result = $this->category_service->update($request->all(), $id);
        return $result;
    }

    /**
     * Remove the specified resource from storage.
     *
     * @param  int  $id
     * @return \Illuminate\Http\Response
     */

    public function updateCategoryPriority(Request $request)
    {
        $result = $this->category_service->updateCategoryPriority($request->all());
        return $result;
    }

    public function destroy($id, Request $request)
    {
        $result = $this->category_service->destroy($id, ($request->has('main_menu')?$request->get('main_menu'):null));
        return $result;
    }
}
