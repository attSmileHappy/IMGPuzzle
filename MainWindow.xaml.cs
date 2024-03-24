using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.XFeatures2D;
using OpenCvSharp.Extensions;
using OpenCvSharp.Features2D;
using System.Drawing;
using System.Linq;

namespace IMGPuzzle
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // 원본 이미지 업로드 버튼 클릭 이벤트 핸들러
        private void UploadOriginalButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                txtOriginalFilePath.Text = filePath;

                DisplayImage(filePath, imgOriginal);
            }
        }

        // 대체 이미지 업로드 버튼 클릭 이벤트 핸들러
        private void UploadReplacementButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true)
            {
                string replacementFilePath = openFileDialog.FileName;
                txtReplacementFilePath.Text = replacementFilePath;

                DisplayImage(replacementFilePath, imgReplacement);
            }
        }

        // 이미지 파일을 이미지 컨트롤에 표시하는 메서드
        private void DisplayImage(string filePath, System.Windows.Controls.Image imageControl)
        {
            BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));
            imageControl.Source = bitmapImage;
        }

        // 이미지 변환 버튼 클릭 이벤트 핸들러
        private void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtOriginalFilePath.Text) || string.IsNullOrWhiteSpace(txtReplacementFilePath.Text))
                {
                    MessageBox.Show("원본 이미지와 변환할 이미지를 업로드하세요.");
                    return;
                }

                // 파일 존재 여부 검증
                if (!File.Exists(txtOriginalFilePath.Text) || !File.Exists(txtReplacementFilePath.Text))
                {
                    MessageBox.Show("선택한 파일이 존재하지 않습니다.");
                    return;
                }


                string tempFilePath = GenerateNewFileName(txtOriginalFilePath.Text); // 새 파일 이름 생성

                MessageBox.Show("이미지 변환을 시작합니다. 잠시 기다려주세요.", "이미지 변환", MessageBoxButton.OK, MessageBoxImage.Information);

                var (keyPointsA, keyPointsB, descriptorsA, descriptorsB) = ExtractKeyPointsAndDescriptors(LoadImageAsMat(txtOriginalFilePath.Text), LoadImageAsMat(txtReplacementFilePath.Text));

                Mat transformedImageB = TransformImage(LoadImageAsMat(txtOriginalFilePath.Text), LoadImageAsMat(txtReplacementFilePath.Text), keyPointsA, keyPointsB, descriptorsA, descriptorsB, tempFilePath);

                transformedImageB.SaveImage(tempFilePath);

                DisplayImage(tempFilePath, imgConverted);

                MessageBox.Show("이미지 변환이 완료되었습니다.", "이미지 변환 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 초기화 버튼 클릭 이벤트 핸들러
        private void InitButton_Click(object sender, RoutedEventArgs e)
        {
            txtOriginalFilePath.Text = null;
            txtReplacementFilePath.Text = null;
            imgOriginal.Source = null;
            imgReplacement.Source = null;
            imgConverted.Source = new BitmapImage(new Uri("/Resource/blank.png", UriKind.Relative)); // Set source to blank.png

            MessageBox.Show("Initialization complete.", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 저장 버튼 클릭 이벤트 핸들러
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (imgConverted.Source == null)
                {
                    MessageBox.Show("변환된 이미지가 없습니다.");
                    return;
                }

                string savePath = ChooseSaveLocation();
                if (string.IsNullOrEmpty(savePath))
                {
                    MessageBox.Show("저장 위치를 선택해주세요.");
                    return;
                }

                string newFilePath = GenerateNewFileName(txtOriginalFilePath.Text); // 새 파일 이름 생성
                TransformAndSaveImages(txtOriginalFilePath.Text, txtReplacementFilePath.Text, savePath, newFilePath);
                DeleteTempFile(newFilePath); // 새로 생성된 파일 삭제
                MessageBox.Show("이미지가 성공적으로 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteTempFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // Check if the file is locked by another process
                    using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        // File is not locked, so close it
                        fs.Close();
                    }

                    // Attempt to delete the file
                    File.Delete(filePath);
                }
            }
            catch (IOException ex)
            {
                MessageBox.Show($"임시 파일을 삭제하는 동안 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateNewFileName(string originalFilePath)
        {
            string directory = Path.GetDirectoryName(originalFilePath);
            string fileName = Path.GetFileNameWithoutExtension(originalFilePath);
            string extension = Path.GetExtension(originalFilePath);

            string currentTime = DateTime.Now.ToString("yyyyMMddHHmmss");
            string newFileName = $"{fileName}_{currentTime}{extension}";

            return Path.Combine(directory, newFileName);
        }

        // 이미지를 변환하고 저장하는 메서드
        private void TransformAndSaveImages(string originalFilePath, string replacementFilePath, string savePath, string newFilePath)
        {
            var (keyPointsA, keyPointsB, descriptorsA, descriptorsB) = ExtractKeyPointsAndDescriptors(LoadImageAsMat(originalFilePath), LoadImageAsMat(replacementFilePath));
            Mat transformedImageB = TransformImage(LoadImageAsMat(originalFilePath), LoadImageAsMat(replacementFilePath), keyPointsA, keyPointsB, descriptorsA, descriptorsB, newFilePath);
            transformedImageB.SaveImage(savePath);
        }

        // 이미지를 Mat 형식으로 로드하는 메서드
        private Mat LoadImageAsMat(string filePath)
        {
            Bitmap bitmap = new Bitmap(filePath);
            return OpenCvSharp.Extensions.BitmapConverter.ToMat(bitmap);
        }

        // 이미지 변환 메서드
        private Mat TransformImage(Mat imageA, Mat imageB, KeyPoint[] keyPointsA, KeyPoint[] keyPointsB, Mat descriptorsA, Mat descriptorsB, string savePath)
        {
            // 매칭을 위한 Matcher 생성
            BFMatcher matcher = new BFMatcher
                (NormTypes.L2);

            // 매칭 수행
            DMatch[] matches = matcher.Match(descriptorsA, descriptorsB);

            // RANSAC 알고리즘을 사용하여 이상치에 강건한 Homography 계산
            Mat homography = Cv2.FindHomography(
                InputArray.Create(matches.Select(match => keyPointsB[match.TrainIdx].Pt).ToArray()),
                InputArray.Create(matches.Select(match => keyPointsA[match.QueryIdx].Pt).ToArray()),
                HomographyMethods.Ransac, 3.0);

            // 이미지 변환
            Mat transformedImageB = new Mat();
            Cv2.WarpPerspective(imageB, transformedImageB, homography, imageA.Size());

            // 저장된 파일 이름으로 이미지 저장
            transformedImageB.SaveImage(savePath);

            return transformedImageB;
        }

        private (KeyPoint[], KeyPoint[], Mat, Mat) ExtractKeyPointsAndDescriptors(Mat imageA, Mat imageB)
        {
            var sift = SIFT.Create();

            KeyPoint[] keyPointsA, keyPointsB;
            Mat descriptorsA = new Mat(), descriptorsB = new Mat();

            // 이미지 A의 키포인트 및 디스크립터 검출
            sift.DetectAndCompute(imageA, null, out keyPointsA, descriptorsA);

            // 이미지 B의 키포인트 및 디스크립터 검출
            sift.DetectAndCompute(imageB, null, out keyPointsB, descriptorsB);

            return (keyPointsA, keyPointsB, descriptorsA, descriptorsB);
        }

        // 사용자가 저장할 위치를 선택하는 메서드
        private string ChooseSaveLocation()
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "JPEG Image|*.jpg|PNG Image|*.png|All Files|*.*";
            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            return null;
        }
    }
}
