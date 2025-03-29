from setuptools import setup, find_packages

setup(
    name="mitm_modular",
    version="0.1.0",
    packages=find_packages(),
    install_requires=[
        "mitmproxy",
        "tabulate",
    ],
    author="Sten",
    author_email="sten@null.net",
    description="A modular MITM proxy system for modifying API responses",
    keywords="mitm, proxy, api, json",
    url="https://github.com/SalamiSimon/mitm_modular",
    classifiers=[
        "Development Status :: 3 - Alpha",
        "Intended Audience :: Developers",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.6",
        "Programming Language :: Python :: 3.7",
        "Programming Language :: Python :: 3.8",
        "Programming Language :: Python :: 3.9",
    ],
    python_requires=">=3.6",
    entry_points={
        "console_scripts": [
            "mitm-modular=mitm_modular.cli:main",
        ],
    },
) 