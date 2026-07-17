variable "region" {
  description = "OCI region, for example us-ashburn-1."
  type        = string
}

variable "oci_auth" {
  description = "OCI Terraform provider auth mode. Use SecurityToken for oci session authenticate profiles."
  type        = string
  default     = "SecurityToken"
}

variable "oci_config_file_profile" {
  description = "OCI CLI config profile used by Terraform."
  type        = string
  default     = "DEFAULT"
}

variable "compartment_id" {
  description = "Compartment OCID where the managed Kafka cluster will be created."
  type        = string
}

variable "subnet_ocids" {
  description = "Subnet OCIDs where Kafka broker/coordinator VNICs will be created. For a simple OKE PoC, use one private subnet reachable from OKE."
  type        = list(string)

  validation {
    condition     = length(var.subnet_ocids) > 0
    error_message = "Provide at least one subnet OCID."
  }
}

variable "display_name" {
  description = "Kafka cluster display name."
  type        = string
  default     = "kafka-poc-oke"
}

variable "cluster_type" {
  description = "DEVELOPMENT for PoC/starter, PRODUCTION for HA."
  type        = string
  default     = "DEVELOPMENT"

  validation {
    condition     = contains(["DEVELOPMENT", "PRODUCTION"], var.cluster_type)
    error_message = "cluster_type must be DEVELOPMENT or PRODUCTION."
  }
}

variable "kafka_version" {
  description = "Kafka version supported by OCI Streaming with Apache Kafka."
  type        = string
  default     = "3.9.1"
}

variable "coordination_type" {
  description = "Kafka coordination type. Kafka 4.0.0 supports KRAFT only."
  type        = string
  default     = "KRAFT"

  validation {
    condition     = contains(["KRAFT", "ZOOKEEPER"], var.coordination_type)
    error_message = "coordination_type must be KRAFT or ZOOKEEPER."
  }
}

variable "broker_node_count" {
  description = "Number of Kafka brokers. Use 3 for production-like PoC; use 1 only for connectivity/cost checks."
  type        = number
  default     = 3

  validation {
    condition     = var.broker_node_count >= 1 && var.broker_node_count <= 30
    error_message = "broker_node_count must be between 1 and 30."
  }
}

variable "broker_ocpu_count" {
  description = "OCPUs per broker."
  type        = number
  default     = 1

  validation {
    condition     = var.broker_ocpu_count >= 1
    error_message = "broker_ocpu_count must be at least 1."
  }
}

variable "broker_node_shape" {
  description = "Broker compute shape."
  type        = string
  default     = "VM.Standard.E5.Flex"
}

variable "broker_storage_size_in_gbs" {
  description = "Block volume storage per broker."
  type        = number
  default     = 150

  validation {
    condition     = var.broker_storage_size_in_gbs >= 50 && var.broker_storage_size_in_gbs <= 16000
    error_message = "broker_storage_size_in_gbs must be between 50 and 16000."
  }
}

variable "allow_everyone_if_no_acl_found" {
  description = "For first PoC smoke tests this can stay true. Set false before validating real security boundaries."
  type        = bool
  default     = true
}

variable "auto_create_topics_enable" {
  description = "Keep false for controlled topic creation."
  type        = bool
  default     = false
}

variable "message_max_bytes" {
  description = "Kafka message.max.bytes broker property."
  type        = number
  default     = 1048576
}

variable "extra_cluster_properties" {
  description = "Additional configurable Kafka broker properties."
  type        = map(string)
  default     = {}
}

variable "enable_sasl_scram_superuser" {
  description = "Enable SASL/SCRAM superuser on the cluster."
  type        = bool
  default     = true
}

variable "create_sasl_scram_secret" {
  description = "Create a Vault, AES key, and initial secret for the Kafka SASL/SCRAM superuser when sasl_scram_secret_id is empty."
  type        = bool
  default     = true
}

variable "sasl_scram_secret_id" {
  description = "OCI Vault secret OCID to be populated/used for the generated SASL/SCRAM superuser password. Leave empty to skip explicit secret binding."
  type        = string
  default     = ""
}

variable "sasl_scram_secret_compartment_id" {
  description = "Compartment OCID containing the Vault secret. Defaults to compartment_id when empty."
  type        = string
  default     = ""
}

variable "sasl_scram_vault_name" {
  description = "Display name for the Vault created for Kafka PoC SASL/SCRAM credentials."
  type        = string
  default     = "kafka-poc-vault"
}

variable "sasl_scram_key_name" {
  description = "Display name for the Vault key created for Kafka PoC SASL/SCRAM credentials."
  type        = string
  default     = "kafka-poc-key"
}

variable "sasl_scram_secret_name" {
  description = "Secret name for Kafka PoC SASL/SCRAM generated superuser password."
  type        = string
  default     = "kafka-poc-superuser"
}

variable "freeform_tags" {
  description = "Freeform tags applied to Kafka resources."
  type        = map(string)
  default = {
    environment = "poc"
    owner       = "platform"
  }
}
