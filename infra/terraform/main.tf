terraform {
  required_providers {
    multipass = {
      source  = "larstobi/multipass"
      version = "~> 1.4.2"
    }
  }
}

provider "multipass" {}

resource "local_file" "cloud_init" {
  content  = <<-EOF
    #cloud-config
    users:
      - default
      - name: ubuntu
        sudo: ALL=(ALL) NOPASSWD:ALL
        ssh_authorized_keys:
          - ${file(pathexpand("~/.ssh/id_rsa.pub"))}
  EOF
  filename = "${path.module}/cloud-init.yaml"
}

resource "multipass_instance" "worker" {
  count          = 2
  name           = "k3s-worker-${count.index + 1}"
  cpus           = 2
  memory         = "2048M"
  disk           = "15G"
  image          = "jammy"
  cloudinit_file = local_file.cloud_init.filename

  depends_on = [local_file.cloud_init]
}

output "worker_ips" {
  value = [for instance in multipass_instance.worker : instance.ipv4]
}