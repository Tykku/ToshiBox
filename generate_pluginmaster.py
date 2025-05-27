import json
import os
from os.path import getmtime
from zipfile import ZipFile

# Direct download URL for your repo structure (no /plugins/ subfolder)
DOWNLOAD_URL = 'https://tykku.github.io/ToshiBox/release/{plugin_name}.zip'

DEFAULTS = {
    'IsHide': False,
    'IsTestingExclusive': False,
    'ApplicableVersion': 'any',
}

DUPLICATES = {
    'DownloadLinkInstall': ['DownloadLinkTesting', 'DownloadLinkUpdate'],
}

TRIMMED_KEYS = [
    'Author',
    'Name',
    'Punchline',
    'Description',
    'Changelog',
    'InternalName',
    'AssemblyVersion',
    'RepoUrl',
    'ApplicableVersion',
    'Tags',
    'DalamudApiLevel',
    'LoadPriority',
    'IconUrl',
    'ImageUrls',
]

def main():
    master = extract_manifests()
    master = [trim_manifest(m) for m in master]
    add_extra_fields(master)
    write_master(master)
    update_last_updated(master)

def extract_manifests():
    manifests = []
    # Walk release folder for zip files
    for filename in os.listdir('./release'):
        if not filename.endswith('.zip'):
            continue
        plugin_name = filename[:-4]  # strip ".zip"
        zip_path = os.path.join('./release', filename)
        with ZipFile(zip_path) as z:
            manifest_json_name = f'{plugin_name}.json'
            if manifest_json_name not in z.namelist():
                print(f"Warning: {manifest_json_name} not found in {zip_path}")
                continue
            manifest = json.loads(z.read(manifest_json_name).decode('utf-8'))
            manifests.append(manifest)
    return manifests

def add_extra_fields(manifests):
    for manifest in manifests:
        manifest['DownloadLinkInstall'] = DOWNLOAD_URL.format(plugin_name=manifest["InternalName"])
        for k, v in DEFAULTS.items():
            manifest.setdefault(k, v)
        for source, keys in DUPLICATES.items():
            for k in keys:
                manifest.setdefault(k, manifest.get(source, ''))
        manifest['DownloadCount'] = 0

def trim_manifest(plugin):
    return {k: plugin[k] for k in TRIMMED_KEYS if k in plugin}

def write_master(master):
    with open('pluginmaster.json', 'w', encoding='utf-8') as f:
        json.dump(master, f, indent=4)

def update_last_updated(master):
    for plugin in master:
        zip_path = os.path.join('release', f'{plugin["InternalName"]}.zip')
        if os.path.exists(zip_path):
            modified = int(getmtime(zip_path))
            if 'LastUpdate' not in plugin or int(plugin['LastUpdate']) != modified:
                plugin['LastUpdate'] = str(modified)
        else:
            print(f"Warning: {zip_path} not found for last updated time")
    write_master(master)

if __name__ == '__main__':
    main()
