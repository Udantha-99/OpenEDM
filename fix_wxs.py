import re
import os

filepath = 'installer/Product.wxs'
with open(filepath, 'r', encoding='utf-8') as f:
    data = f.read()

# Remove OpenEDMPrivateKey component
data = re.sub(
    r'<Component Id="OpenEDMPrivateKey".*?</Component>',
    '',
    data,
    flags=re.DOTALL
)

# Remove the permission element for OpenEDMConfigFile
data = re.sub(
    r'<Permission User="Users" GenericAll="yes"\s*/>',
    '',
    data
)

with open(filepath, 'w', encoding='utf-8') as f:
    f.write(data)

print("Product.wxs updated.")
