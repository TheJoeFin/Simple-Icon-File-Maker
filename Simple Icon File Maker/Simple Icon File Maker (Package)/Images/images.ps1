# Define the directory containing the images
$directoryPath = ""

# Get all image files in the directory
$imageFiles = Get-ChildItem -Path $directoryPath -Include @("*.jpg", "*.jpeg", "*.png", "*.gif") -Recurse

# Create a CSV file to store image names and dimensions
$outputCsvPath = $directoryPath + "\images_dimensions.csv"

# Initialize an empty array to hold the image data
$imageData = @()

foreach ($image in $imageFiles) {
    # Load the image to get its dimensions
    $img = [System.Drawing.Image]::FromFile($image.FullName)
    
    # Create a custom object with the image name and dimensions
    $data = [PSCustomObject]@{
        Name   = $image.Name
        Width  = $img.Width
        Height = $img.Height
    }
    
    # Add the custom object to the array
    $imageData += $data
    
    # Dispose the image object to free resources
    $img.Dispose()
}

# Export the array of image data to a CSV file
$imageData | Export-Csv -Path $outputCsvPath -NoTypeInformation

Write-Host "CSV file has been created at $outputCsvPath"