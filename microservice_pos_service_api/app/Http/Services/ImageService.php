<?php

namespace App\Http\Services;

use Storage;
use Illuminate\Http\File;
use App\microservice_delivergate_api\Services\BaseService as BaseService;
use Illuminate\Support\Facades\Config;
use Intervention\Image\Facades\Image;

class ImageService extends BaseService
{
    public function uploadImageByURL($image, $destination)
    {
        $filename = time() . rand(10, 1000);
        $ext = pathinfo($image, PATHINFO_EXTENSION);
        $imgOri = Image::make($image);
        $imgPath = $destination . '/' . $filename . '.' . $ext;
        $imgOri->save(public_path($imgPath));
        return $imgPath;
    }

    public function uploadImageByFile($image, $destination, $width = null)
    {
        $filename = time() . rand(10, 1000);
        $imgOri = Image::make($image);
        $ext = $image->getClientOriginalExtension();
        $imgPath = $destination . '/' . $filename . '.' . $ext;
        if (!is_null($width)) {
            $imgOri = $imgOri->resize($width, null, function ($constraint) {
                $constraint->aspectRatio();
            });
        }
        if (Config::get('app.env') == 'production') {
            $imgOri->save($imgPath);
        } else {
            $imgOri->save(public_path($imgPath));
        }
        return $imgPath;
    }

    public function uploadImageByFileSize($image, $destination)
    {
        $imageSizes = ['original' => 1200, 'medium' => 800];
        $watermark = Image::make('images/logo.png');
        $filename = time() . rand(10, 1000);

        $imgOri = Image::make($image);
        $ext = $image->getClientOriginalExtension();
        $imgOri->insert($watermark, 'bottom-right', 10, 10);
        $rtnPaths = [];
        foreach ($imageSizes as $key => $imgSize) {
            $imgTemp = $imgOri;
            $imgPath = $destination . '/' . $filename . '_' . $key . '.' . $ext;
            $imgTemp = $imgTemp->resize($imgSize, null, function ($constraint) {
                $constraint->aspectRatio();
            });
            if (Config::get('app.env') == 'production') {
                $imgTemp->save($imgPath);
            } else {
                $imgTemp->save(public_path($imgPath));
            }

            $rtnPaths[$key] = $imgPath;
        }
        return $rtnPaths;
    }

    public function uploadImageToCloud($image, $destination)
    {
        $path = $image->storePubliclyAs(
            Config::get('filesystems.disks.s3.cloud_subdirectory') . '/' . $destination,
            time() . rand(10, 1000) . '.' . $image->getClientOriginalExtension(),
            's3'
        );
        $fullPath = 'https://s3-' . Config::get('filesystems.disks.s3.region') . '.amazonaws.com/' . Config::get('filesystems.disks.s3.bucket') . '/' . $path;
        return $fullPath;
    }

    public function deleteImageFromS3($url)
    {
        $url = str_replace('https://s3-' . Config::get('filesystems.disks.s3.region') . '.amazonaws.com/' . Config::get('filesystems.disks.s3.bucket'), '', $url);
        $rtn = Storage::disk('s3')->delete($url);
        return $rtn;
    }

    public function resizeAndUploadImageToCloud($image, $destination)
    {
        $imageSizes = ['large' => 1920, 'medium' => 512, 'small' => 140];
        $filename = time() . rand(10, 1000);
        $imgOri = Image::make($image);
        $ext = $image->getClientOriginalExtension();
        $images = [];
        foreach ($imageSizes as $key => $size) {
            $imgPath =  $filename .'-'.$size. '.' . $ext;
            $imgOri = $imgOri->resize($size, null, function ($constraint) {
                $constraint->aspectRatio();
            });
            $imgOri->save(storage_path('app/'.$imgPath));

            $path = Storage::disk('s3')->putFileAs(Config::get('filesystems.disks.s3.cloud_subdirectory'). '/' . $destination, new File(storage_path('app/'.$imgPath)), $imgPath, 'public');
            
            $images[$key] = 'https://s3-' . Config::get('filesystems.disks.s3.region') . '.amazonaws.com/' . Config::get('filesystems.disks.s3.bucket') . '/' . $path;
            Storage::delete($imgPath);
        }
        return $images;
    }



    public function resizeAndUploadImageToCloudByUrl($image, $destination)
    {
        $imageSizes = ['large' => 1920, 'medium' => 512, 'small' => 140];
        $filename = time() . rand(10, 1000);
        $imgOri = Image::make($image);
        $ext = pathinfo($image, PATHINFO_EXTENSION);
        $images = [];
        foreach ($imageSizes as $key => $size) {
            $imgPath =  $filename .'-'.$size. '.' . $ext;
            $imgOri = $imgOri->resize($size, null, function ($constraint) {
                $constraint->aspectRatio();
            });
            $imgOri->save(storage_path('app/'.$imgPath));

            $path = Storage::disk('s3')->putFileAs(Config::get('filesystems.disks.s3.cloud_subdirectory'). '/' . $destination, new File(storage_path('app/'.$imgPath)), $imgPath, 'public');
            
            $images[$key] = 'https://s3-' . Config::get('filesystems.disks.s3.region') . '.amazonaws.com/' . Config::get('filesystems.disks.s3.bucket') . '/' . $path;
            Storage::delete($imgPath);
        }
        return $images;
    }
}
