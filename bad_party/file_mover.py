import os
import requests

DIRECTORY_PATH = '../../../Documents/Unreal Projects/NiceGuy/Content/Dialogue'

def download_wav(url):
    try:
        if not os.path.exists(DIRECTORY_PATH):
            os.makedirs(DIRECTORY_PATH)
        
        file_name = url.split('/')[-1]
        
        save_path = os.path.join(DIRECTORY_PATH, file_name)
        
        response = requests.get(url)
        response.raise_for_status()  # Raise an exception for HTTP errors
        
        with open(save_path, 'wb') as f:
            f.write(response.content)
        
        print(f"File downloaded and saved to {save_path}")
    except requests.RequestException as e:
        print(f"An error occurred while downloading the file: {e}")
    except Exception as e:
        print(f"An error occurred: {e}")

