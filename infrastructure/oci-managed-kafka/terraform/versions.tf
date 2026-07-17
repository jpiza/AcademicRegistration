terraform {
  required_version = ">= 1.5.0"

  required_providers {
    oci = {
      source  = "oracle/oci"
      version = ">= 7.18.0, < 8.0.0"
    }
  }
}

provider "oci" {
  auth                = var.oci_auth
  config_file_profile = var.oci_config_file_profile
  region              = var.region
}
