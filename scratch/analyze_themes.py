import os
import re

themes_dir = r"c:\Users\D\Documents\Skola\Workshop\TEST K4U2\K4U2 - AI Content Assistant\Site\template\themes"
base_css_path = r"c:\Users\D\Documents\Skola\Workshop\TEST K4U2\K4U2 - AI Content Assistant\Site\template\styles.css"

def parse_css_rules(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()
    
    # Strip comments
    content = re.sub(r"/\*.*?\*/", "", content, flags=re.DOTALL)
    
    # Extract rules
    rules = {}
    # Simple regex to split rules
    matches = re.finditer(r"([^{]+)\{(.*?)\}", content, flags=re.DOTALL)
    for m in matches:
        selectors = m.group(1).strip()
        body = m.group(2).strip()
        rules[selectors] = body
    return rules

base_rules = parse_css_rules(base_css_path)
print(f"Base CSS rules: {len(base_rules)}")

for file_name in os.listdir(themes_dir):
    if file_name.endswith(".css"):
        theme_path = os.path.join(themes_dir, file_name)
        theme_rules = parse_css_rules(theme_path)
        print(f"Theme {file_name}: {len(theme_rules)} rules")
        
        # Check if theme rules are subset of base or have unique selectors
        unique_selectors = []
        for selector in theme_rules:
            if selector == ":root" or "body" in selector or "h1" in selector or "h2" in selector or "h3" in selector:
                continue
            if selector not in base_rules:
                unique_selectors.append(selector)
        
        if unique_selectors:
            print(f"  Unique selectors in {file_name}: {len(unique_selectors)}")
            print(f"  Sample: {unique_selectors[:5]}")
