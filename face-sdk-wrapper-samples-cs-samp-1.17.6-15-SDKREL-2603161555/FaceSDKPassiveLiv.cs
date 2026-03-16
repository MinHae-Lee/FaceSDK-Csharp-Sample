using System;

/*
   260105 facesdk.EstimateFaceQuality
          > discard calling param 'detected_face_landmark', 'detected_face_landmark5pt'
*/


namespace Alchera.FaceSDK
{
    public class LivenessThreshold
    {
        // check camera source
        public const int SRC_IMG_W_MIN = 1280;
        public const int SRC_IMG_H_MIN = 720;

        // minimum face width for liveness
        public const float FACE_RATIO_WIDTH_MIN = 0.26F; // align to server compare api (0.25 ~ 1.0)
        public const float FACE_RATIO_WIDTH_MAX = 0.6F;

        // for left-right cropped img by aspect 720:1280
        // > new_width = cur_height * (720/1280);
        public const float SRC_CROP_ASPECT_HEIGHT = 720F;
        public const float SRC_CROP_ASPECT_WIDTH = 1280F;

        public const float FACE_RATIO_WIDTH_MIN_FOR_CROP_IMG = 0.4F; // 0.25~1.0, def: 0.4, 0.5: face-width:>200 (x720)
        public const float FACE_RATIO_WIDTH_MAX_FOR_CROP_IMG = 0.6F;

        // image center positioned ratio for close to the center
        // def=0.2, 0.1: to center high, 0.2: align center low
        public const float CENTER_FACE_W_H_POS_RATIO = 0.1F;

        // yaw/pitch/roll
        public static readonly float[] FACE_YAW = { -20.0F, 20.0F };    // min,max
        public static readonly float[] FACE_PITCH = { -10.0F, 25.0F };  // min,max
        public static readonly float[] FACE_ROLL = { -20.0F, 20.0F };  // min,max

        // attribute
        public const float ATTR_FACE_IS_MASKED = FaceSDK.Params.THRESHOLD_ATTR_FACE_IS_MASKED; // 0.5F;

        // occlusion: score > ATTR_FACE_FINE_OCCLUSION
        public const float ATTR_FACE_FINE_OCCLUSION = 0.8221F;
        public static bool IsFaceFineOcclusion(float score) => score > ATTR_FACE_FINE_OCCLUSION;

        // Face Feature Quality, [0] unmasked face , masked face
        // server :  { 63.96F, 65.86F }
        // min: { 56.96F, 58.86F } 
        public static readonly float[] FACE_FEATURE_QUALITY = { 63.96F, 65.86F };

        // Face Antispoofing Quality - FAS-QUALITY
        public const float FACE_ANTISPOOFING_QUALITY = 0.5648F;

        // BGR Image Liveness
        public const float BGR_IMG_LIVENESS = 0.8808F;
    }

    public class PassiveLivness
    {
        public enum Error : int
        {
            NoError,

            NoFaceDetected,             // no face in given source image
            MultipleFacesDetected,      // Multiple faces detected, only one face is allowed during liveness operation 

            FaceIsMasked,               // Take off face mask

            // check detected face width/heigth with entire image width/height
            FaceWidthRatioSmall,        // Please Move Closer to the camera 
            FaceWidthRatioLarge,        // Please Move Away to the camera

            // To prevent liveness value distortion caused by camera distortion, we need to center the face.
            FacePosXRatioToCenter,      // plase move face x(horizontal) pos to center
            FacePosYRatioToCenter,      // plase move face y(vertical) pos to center

            FaceYaw,                    // failed to check face Yaw range
            FacePitch,                  // failed to check face Pitch range
            FaceRoll,                   // failed to check face Roll range

            FaceQuality,         // face quality is too low to extract feature vector(512D) , face detection quality

            FASQuality,                 // FAS(Face AntiSpoofing) quality is too low,
                                        // > whether the image is suitable for liveness detection
                                        // > checking background light, reflection of display device(check image on display)

            BGRImageQuality,            // BRG(blue/green/red) image quality is too low for liveness operation


            // FaceSDK
            FaceSDKInvalid = 1000,         // Invalid FaceSDK
            FaceSDKOperFail = 1100,        // see faceSDK last error

            Unknown = 9999,
        }


        //
        // result
        //

        public class Result
        {
            public Error liveness_err_ = Error.NoError;
            public string liveness_err_desc_ = "";

            public bool is_liveness_ok_ = false;
            public float liveness_confidence_ = 0.0f;

            public FaceSDK.Face detected_face_;
            public int detected_face_cnt_;

            public bool is_face_masked_ = false;

            public byte[] bgr_pixels_;
            public int bgr_width_;
            public int bgr_height_;

            public String last_err_msg1_ = "";
            public String last_err_msg2_ = "";
            public String last_err_msg3_ = "";

            public void SetErrMsg(Error err = Error.NoError, String msg1 = "", String msg2 = "", String msg3 = "")
            {
                liveness_err_ = err;
                liveness_err_desc_ = msg1;

                is_liveness_ok_ = false;

                last_err_msg1_ = msg1;
                last_err_msg2_ = msg2;
                last_err_msg3_ = msg3;
            }

        }

        public static Result DoPassiveLiveness(
            FaceSDK facesdk,
            byte[] img_pixels_bgr, int img_width, int img_height,
            float threshold_face_width_ratio_min, // LivenessThreshold.FACE_RATIO_WIDTH_MIN , _FOR_CROP_IMG
            float threshold_face_width_ratio_max, // LivenessThreshold.FACE_RATIO_WIDTH_MAX , _FOR_CROP_IMG
            bool use_continuous_img_face_detect, // false , [false] detect face in single image
            bool check_valid_face_width_ratio_for_liveness, // true
            bool check_center_face_position_in_img, // true
            float threshold_center_face_w_h_pos_ratio, // LivenessThreshold.CENTER_FACE_W_H_POS_RATIO
            bool check_face_yaw_pitch_roll, // true
            float[] threshold_face_yaw,    // LivenessThreshold.FACE_YAW, [0] min [1] max
            float[] threshold_face_pitch,  // LivenessThreshold.FACE_PITCH
            float[] threshold_face_roll,   // LivenessThreshold.FACE_ROLL
            bool check_face_is_masked, // true
            bool check_feature_quality, // true
            float[] threshold_feature_quality, // LivenessThreshold.THRESHOLD_FACE_FEATURE_QUALITY, Face Detection Quality
            bool check_fas_quality, // true
            float threshold_fas_quality,  // LivenessThreshold.FACE_ANTISPOOFING_QUALITY
            float threshold_bgr_img_liveness, // LivenessThreshold.BGR_IMG_LIVENESS
            bool check_multiple_faces = true // true=only one face is allowed during liveness operation 
        )
        {
            Result rst = new Result();

            //
            // detect face
            //

            FaceSDK.Face detected_face;
            FaceSDK.LandMark detected_face_landmark;
            FaceSDK.LandMark5pt detected_face_landmark5pt;

            FaceSDK.Rst detect_rst = facesdk.DetectFace(img_pixels_bgr, img_width, img_height, use_continuous_img_face_detect);

            if (detect_rst.IsErr())
            {
                rst.SetErrMsg(Error.FaceSDKOperFail, detect_rst.GetLastErrStr() + ",FaceSDK.DetectFace");
                return rst;
            }

            int detected_face_cnt = detect_rst.GetFaceCnt();

            if (detected_face_cnt == 0)
            {
                rst.SetErrMsg(Error.NoFaceDetected, "no face is detected");
                return rst;
            }

            detected_face = detect_rst.GetFace();
            detected_face_landmark = detected_face.landmark;
            detected_face_landmark5pt = detected_face.landmark_5pt;
            //FaceSDK.Print(detected_face);

            rst.detected_face_ = detect_rst.face;
            rst.detected_face_cnt_ = detected_face_cnt;

            if (check_multiple_faces)
            {
                if (detected_face_cnt > 1)
                {
                    rst.SetErrMsg(Error.MultipleFacesDetected, "multiple faces detected",
                        "detected face cnt=" + detected_face_cnt + " > 1",
                        "ONLY ONE FACE IS ALLOWED DURING LIVENESS!!"
                        );
                    return rst;
                }
            }


            //
            // check face masked
            //

            bool is_face_masked = false;

            if (check_face_is_masked)
            {
                FaceSDK.Rst face_masked_rst = facesdk.CheckMask(
                      img_pixels_bgr, img_width, img_height, ref detected_face);

                if (face_masked_rst.IsErr())
                {
                    rst.SetErrMsg(Error.FaceIsMasked, face_masked_rst.GetLastErrStr() + ",FaceSDK.CheckMask");
                    return rst;
                }

                is_face_masked = face_masked_rst.IsFaceMasked();
                rst.is_face_masked_ = is_face_masked;
            }


            //
            // check detected face width/heigth with entire image width/height
            //

            if (check_valid_face_width_ratio_for_liveness)
            {
                float width_ratio = detected_face.box.w / img_width;

                if (width_ratio < threshold_face_width_ratio_min)
                {
                    rst.SetErrMsg(Error.FaceWidthRatioSmall, "face width ratio is too small",
                        "width_ratio: " + width_ratio.ToString("F2") + "  < [thres] " + threshold_face_width_ratio_min,
                        "Please Move Closer to the camera !"
                    );

                    return rst;
                }

                if (width_ratio > threshold_face_width_ratio_max)
                {
                    rst.SetErrMsg(Error.FaceWidthRatioLarge, "face width ratio is too large",
                        "width_ratio: " + width_ratio.ToString("F2") + " > [thres] " + threshold_face_width_ratio_max,
                        "Please Move Away to the camera !"
                    );

                    return rst;
                }
            }


            //
            // To prevent liveness value distortion caused by camera distortion, we need to center the face.
            //

            if (check_center_face_position_in_img)
            {
                float img_center_x = img_width / 2;
                float img_center_y = img_height / 2;

                float face_center_x = detected_face.box.x + (detected_face.box.w / 2);
                float face_center_y = detected_face.box.y + (detected_face.box.h / 2);

                float center_diff_x = Math.Abs(img_center_x - face_center_x);
                float center_diff_y = Math.Abs(img_center_y - face_center_y);

                float valid_width_ratio = img_width * threshold_center_face_w_h_pos_ratio;
                float valid_height_ratio = img_height * threshold_center_face_w_h_pos_ratio;

                bool valid_x_pos = center_diff_x < valid_width_ratio;
                bool valid_y_pos = center_diff_y < valid_height_ratio;

                if (!valid_x_pos)
                {
                    rst.SetErrMsg(Error.FacePosXRatioToCenter, "fail on center face x pos ratio",
                        "center_x_ratio=" + center_diff_x.ToString("F2") + " < [thres] " + valid_width_ratio.ToString("F2"),
                        "Plase Move Face X Pos To Center!"
                    );
                    return rst;
                }

                if (!valid_y_pos)
                {
                    rst.SetErrMsg(Error.FacePosYRatioToCenter, "fail on center face y pos ratio",
                        "center_y_ratio=" + center_diff_y.ToString("F2") + " < [thres] " + valid_height_ratio.ToString("F2"),
                        "Plase Move Face Y Pos To Center!"
                    );
                    return rst;
                }
            }


            //
            // check face YAW / PITCH / ROLL
            //

            if (check_face_yaw_pitch_roll)
            {
                if (detected_face.pose.yaw <= threshold_face_yaw[0]
                    || detected_face.pose.yaw >= threshold_face_yaw[1])
                {
                    rst.SetErrMsg(Error.FaceYaw, "face-yaw is out of range",
                        "[min] " + threshold_face_yaw[0] + " >= [cur] " + detected_face.pose.yaw.ToString("F2") + " <= [max] " + threshold_face_yaw[1]);

                    return rst;
                }

                if (detected_face.pose.pitch <= threshold_face_pitch[0]
                    || detected_face.pose.pitch >= threshold_face_pitch[1])
                {
                    rst.SetErrMsg(Error.FacePitch, "face-pitch is out of range",
                        "[thres_min] " + threshold_face_pitch[0] + " >= [cur] " + detected_face.pose.pitch.ToString("F2") + " <= [max] " + threshold_face_pitch[1]);

                    return rst;
                }

                if (detected_face.pose.roll <= threshold_face_roll[0]
                    || detected_face.pose.roll >= threshold_face_roll[1])
                {
                    rst.SetErrMsg(Error.FaceRoll, "face-roll is out of range",
                        "[min] " + threshold_face_roll[0] + " >= [cur] " + detected_face.pose.roll.ToString("F2") + " <= [max] " + threshold_face_roll[1]);

                    return rst;
                }
            }


            //
            // Quality check whether face is in image is suitable for feature extract and matching
            // > Face Quality, Face Detection Quality
            //

            if (check_feature_quality)
            {
                FaceSDK.Rst estimate_face_quality_rst = facesdk.EstimateFaceQuality(
                      img_pixels_bgr, img_width, img_height, ref detected_face, is_face_masked);

                if (estimate_face_quality_rst.IsErr())
                {
                    rst.SetErrMsg(Error.FaceSDKOperFail,
                        estimate_face_quality_rst.GetLastErrStr() + ",FaceSDK.EstimateFaceQuality");

                    return rst;
                }

                float feature_quality = estimate_face_quality_rst.GetFaceQualityForFeature();

                if (feature_quality < threshold_feature_quality[is_face_masked ? 1 : 0])
                {
                    rst.SetErrMsg(Error.FaceQuality, "face quality(feature quality) is not good!",
                        feature_quality.ToString("F2") + " < [thres,face_masked=" + is_face_masked + "] " + threshold_feature_quality[is_face_masked ? 1 : 0]);

                    return rst;
                }
            }

            //
            // Face Antispoofing Quality check 
            // > whether the image is suitable for liveness detection
            //

            if (check_fas_quality)
            {
                FaceSDK.Rst img_face_quality_for_liveness_rst = facesdk.GetImgFaceQualityForLiveness(
                      img_pixels_bgr, img_width, img_height, ref detected_face);

                if (img_face_quality_for_liveness_rst.IsErr())
                {
                    rst.SetErrMsg(Error.FaceSDKOperFail,
                        img_face_quality_for_liveness_rst.GetLastErrStr() + ",FaceSDK.GetImgFaceQualityForLiveness");

                    return rst;
                }

                float img_fas_quality = img_face_quality_for_liveness_rst.GetFaceQualityForLiveness();

                if (img_fas_quality < threshold_fas_quality)
                {
                    rst.SetErrMsg(Error.FASQuality, "FAS(Face AntiSpoofing) quality(light,display dev) is not good",
                        img_fas_quality.ToString("F2") + " < [thres] " + threshold_fas_quality);

                    return rst;
                }
            }

            //
            // get bgr image liveness
            //

            FaceSDK.Rst img_liveness_rst = facesdk.GetImgLiveness(
                  img_pixels_bgr, img_width, img_height, ref detected_face);

            if (img_liveness_rst.IsErr())
            {
                rst.SetErrMsg(Error.FaceSDKOperFail, img_liveness_rst.GetLastErrStr() + ",FaceSDK.GetImgLiveness");

                return rst;
            }

            float img_liveness_confidence = img_liveness_rst.GetImgLiveness();

            if (img_liveness_confidence > threshold_bgr_img_liveness)
            {
                //Console.WriteLine("[INFO] image liveness is GOOD");
                //Console.WriteLine("[INFO] > image liveness: " + img_liveness + " > [thres] " + FaceSDK.THRESHOLD_ANTISPOOFING);
            }
            else
            {
                rst.SetErrMsg(Error.BGRImageQuality, "BGR image quality is not good",
                    img_liveness_confidence.ToString("F2") + " <= [thres] " + threshold_bgr_img_liveness);

                return rst;
            }

            rst.is_liveness_ok_ = true;
            rst.liveness_confidence_ = img_liveness_confidence;

            return rst;
        }
    }
}
