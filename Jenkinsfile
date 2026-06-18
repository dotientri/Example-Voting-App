pipeline {
  agent any

  options {
    timestamps()
    disableConcurrentBuilds()
  }

  environment {
    DOCKERHUB_REGISTRY = 'docker.io'
    IMAGE_NAMESPACE = 'hiiamgay'
    DOCKERHUB_CREDENTIALS_ID = 'dockerhub-creds'

    K8S_NAMESPACE = 'voting-app'
    K8S_MANIFEST_DIR = 'k8s/manifests'

    VOTE_IMAGE = 'hiiamgay/vote'
    RESULT_IMAGE = 'hiiamgay/result'
    WORKER_IMAGE = 'hiiamgay/worker'
  }

  stages {
    stage('Checkout') {
      steps {
        checkout scm
        script {
          env.IMAGE_TAG = sh(
            script: 'git rev-parse --short=7 HEAD',
            returnStdout: true
          ).trim()
        }
      }
    }

    stage('Login to Docker Hub') {
      steps {
        withCredentials([usernamePassword(
          credentialsId: 'dockerhub-creds',
          usernameVariable: 'DOCKERHUB_USER',
          passwordVariable: 'DOCKERHUB_PASS'
        )]) {
          sh '''
            set -eu
            docker logout "$DOCKERHUB_REGISTRY" || true
            echo "$DOCKERHUB_PASS" | docker login "$DOCKERHUB_REGISTRY" -u "$DOCKERHUB_USER" --password-stdin
          '''
        }
      }
    }

    stage('Build images') {
      steps {
        sh '''
          set -eu
          docker build -t "$VOTE_IMAGE:$IMAGE_TAG" -t "$VOTE_IMAGE:latest" ./vote
          docker build -t "$RESULT_IMAGE:$IMAGE_TAG" -t "$RESULT_IMAGE:latest" ./result
          docker build -t "$WORKER_IMAGE:$IMAGE_TAG" -t "$WORKER_IMAGE:latest" ./worker
        '''
      }
    }

    stage('Login and push images') {
      steps {
        sh '''
          set -eu
          docker push "$VOTE_IMAGE:$IMAGE_TAG"
          docker push "$VOTE_IMAGE:latest"
          docker push "$RESULT_IMAGE:$IMAGE_TAG"
          docker push "$RESULT_IMAGE:latest"
          docker push "$WORKER_IMAGE:$IMAGE_TAG"
          docker push "$WORKER_IMAGE:latest"
        '''
      }
    }

    stage('Deploy to Kubernetes') {
      steps {
        withCredentials([
          string(credentialsId: 'ngrok-token', variable: 'NGROK_AUTHTOKEN'),
          file(credentialsId: 'kubeconfig', variable: 'KUBECONFIG')
        ]) {
          sh '''
            set -eu
            # Xóa các resource cũ trước khi áp dụng manifest mới (bỏ qua nếu không tồn tại)
            echo "Deleting existing resources from $K8S_MANIFEST_DIR (if any)"
            kubectl delete -f "$K8S_MANIFEST_DIR/" --ignore-not-found || true

            # Tạo namespace trước để có thể tạo Secret an toàn
            kubectl create namespace "$K8S_NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -
            # Lấy token từ Jenkins để tạo K8s Secret
            kubectl create secret generic ngrok-token-secret --from-literal=token="$NGROK_AUTHTOKEN" -n "$K8S_NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

            # Áp dụng manifest mới
            kubectl apply -f "$K8S_MANIFEST_DIR/"
            kubectl -n "$K8S_NAMESPACE" set image deployment/vote vote="$VOTE_IMAGE:$IMAGE_TAG"
            kubectl -n "$K8S_NAMESPACE" set image deployment/result result="$RESULT_IMAGE:$IMAGE_TAG"
            kubectl -n "$K8S_NAMESPACE" set image deployment/worker worker="$WORKER_IMAGE:$IMAGE_TAG"
            kubectl -n "$K8S_NAMESPACE" rollout status deployment/vote --timeout=180s
            kubectl -n "$K8S_NAMESPACE" rollout status deployment/result --timeout=180s
            kubectl -n "$K8S_NAMESPACE" rollout status deployment/worker --timeout=180s
          '''
        }
      }
    }
  }

  post {
    always {
      sh 'docker logout "$DOCKERHUB_REGISTRY" || true'
    }
  }
}
