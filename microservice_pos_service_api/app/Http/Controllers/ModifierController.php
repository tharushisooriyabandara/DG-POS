<?php

namespace App\Http\Controllers;

use App\Http\Services\ModifierService;
use Illuminate\Http\Request;

class ModifierController extends Controller
{
    private $modifier_service;

    public function __construct()
    {
        $this->modifier_service = new ModifierService;
    }

    public function index()
    {
        $result = $this->modifier_service->index();
        return $result;
    }

    public function show($id)
    {
        $result = $this->modifier_service->show($id);
        return $result;
    }

    public function update(Request $request, $id)
    {
        $result = $this->modifier_service->update($request->all(), $id);
        return $result;
    }

    public function store(Request $request)
    {
        $result = $this->modifier_service->store($request->all());
        return $result;
    }

    public function modifierGroupItems($id)
    {
        $result = $this->modifier_service->modifierGroupItems($id);
        return $result;
    }

    public function modifierGroupModifierItems($id)
    {
        $result = $this->modifier_service->modifierGroupModifierItems($id);
        return $result;
    }

    public function destroy($id)
    {
        $result = $this->modifier_service->destroy($id);
        return $result;
    }

    public function findModifierFromItemAndModifierItem(Request $request)
    {
        $result = $this->modifier_service->findModifierFromItemAndModifierItem($request->all());
        return $result;
    }
}
